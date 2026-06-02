#include "TcpConnection.h"
#include "logger.h"
#include "Socket.h"
#include "Channel.h"
#include "EventLoop.h"
#include <errno.h>
#include <functional>
#include <unistd.h>

static EventLoop* CheckLoopNotNull(EventLoop* loop){
    if(loop == nullptr){
        LOG_FATAL("%s:%s:%d TcpConnectionLoop is null!\n",__FILE__,__FUNCTION__,__LINE__);
    }
    return loop;
}

TcpConnection::TcpConnection(EventLoop* loop,
        const std::string& name,
        int sockfd,
        const InetAddress& localAddr,
        const InetAddress& peerAddr)
    :loop_(CheckLoopNotNull(loop))
    ,name_(name)
    ,state_(kDisconnecting)
    ,reading_(true)
    ,socket_(new Socket(sockfd))
    ,channel_(new Channel(loop,sockfd))
    ,localAddr_(localAddr)
    ,peerAddr_(peerAddr)
    ,highWaterMark_(64 * 1024 * 1024)//64M
{

    //设置回调，poller返回之后channel执行相应回调
    channel_->setReadCallback(
        [this](Timestamp time) {
            handleRead(time);
        }
    );
    channel_->setWriteCallback(
        [this](){
            handleWrite();
        }
    );
    channel_->setCloseCallback(
        [this](){
            handleClose();
        }
    );
    channel_->setErrorCallback(
        [this](){
            handleError();
        }
    );

    LOG_INFO("TcpConnection::ctor[%s] at fd=%d\n",name.c_str(),sockfd);
    socket_->setKeepAlive(true);
}

TcpConnection::~TcpConnection(){
    LOG_INFO("TcpConnection::dtor[%s] at fd=%d state=%d\n"
        ,name_.c_str(),channel_->getFd(),(int)state_);
}

void TcpConnection::handleRead(Timestamp receiveTime){
    int saveErrno = 0;
    //读事件，读到inputbuffer里面，用户可以调用retrieveAllAsString将缓冲区的数据读出来
    ssize_t n = inputBuffer_.readFd(channel_->getFd(),&saveErrno);
    if(n > 0){
        //已建立连接的用户，有可读事件发生了，调用用户传入的回调操作onMessage
        messageCallback_(shared_from_this() ,&inputBuffer_,receiveTime);
    }
    else if( n == 0){
        handleClose();
    }
    else {
        errno = saveErrno;
        LOG_ERROR("TcpConnection::handleRead\n");
        handleError();
    }

}

void TcpConnection::handleWrite(){
    if(channel_->isWritng()){
        int saveErrno = 0;
        //将outputbuffer中的数据写到内核中
        ssize_t n = outputBuffer_.writeFd(channel_->getFd(),&saveErrno);
        if(n > 0){
            outputBuffer_.retrieve(n);
            if(outputBuffer_.readableBytes() == 0){
                //写完了就不需要在监听写事件
                channel_->disableWriting();
                if(writeCompleteCallback_){
                    //唤醒loop_对应的thread线程，执行回调
                    loop_->queueInLoop(
                        [this](){
                            writeCompleteCallback_(shared_from_this());
                        }
                    );
                }
                if(state_ == kDisconnecting){
                    shutdownInLoop();
                }
            }
        }
        else {
        LOG_ERROR("TcpConnection::handleWrite\n");
        }

    }
    else {
        LOG_ERROR("TcpConnection fd=%d is down,no more writing\n",channel_->getFd());
    }
}

void TcpConnection::handleClose(){
    LOG_INFO("fd=%d state=%d connectionClose\n",channel_->getFd(),(int)state_);
    setState(kDisconnected);
    channel_->disableAll();

    TcpConnectionPtr connPtr(shared_from_this());
    connectionCallback_(connPtr);//执行连接关闭的回调
    closeCallback_(connPtr);//关闭连接的回调

}

void TcpConnection::handleError(){
    int optval;
    socklen_t optlen = sizeof optval;
    int saveErrno = 0;
    if(::getsockopt(channel_->getFd(),SOL_SOCKET,SO_ERROR,&optval,&optlen) < 0){
        saveErrno = errno; 
    } 
    else {
        saveErrno = optval;
    }
    LOG_ERROR("TcpConnection::handleErrno name:%s - so_ERRNO:%d\n",name_.c_str(),saveErrno);

}


/*
发送数据，应用写的快，而内核发送数据慢，需要把代发数据写入缓冲区，而且设置了水位回调
*/
void TcpConnection::sendInLoop(const void* data,std::size_t len){
    ssize_t nwrote = 0;
    size_t remaining  = len;
    bool faultError = false;

    //之前调用过该connection的shutdown,就不能进行发送了
    if(state_ == kDisconnected){
        LOG_ERROR("disconnected,give up writing!\n");
        return ;
    }

    //表示channel_第一次开始写数据，而且缓冲区没有待发送数据
    if(!channel_->isWritng() && outputBuffer_.readableBytes() == 0){
        nwrote = ::write(channel_->getFd(),data,len);

        if(nwrote >= 0){
            remaining = len - nwrote;
            if(remaining == 0 && writeCompleteCallback_){
                //数据全部发送完成，就不用再给channel设置epollout事件了
                loop_->queueInLoop(
                    [this](){
                        writeCompleteCallback_(shared_from_this());
                    }
                );
            }
        }
        else {//nwrote  < 0
            nwrote = 0;
            if(errno != EWOULDBLOCK){
                LOG_ERROR("TcpConnection::sendInLoop\n");
                if(errno == EPIPE || errno == ECONNRESET){//SIGPIPE RESET
                    faultError = true;
                }
            }
        }
    }

    if(!faultError && remaining > 0){
        //说明当前这一次write，并没有把数据发送出去，剩余的数据需要保存到缓冲区当中，然后给channel
        //注册epollout事件，poll发现tcp发送缓冲区有空间，回通知相应的sock-channel,调用writeCallback方法
        //最终也就是调用tcpConnection::handlewrite方法，把发送缓冲区中剩余的数据发送完成
        //还是没有完成就继续这个循环
        size_t oldLen = outputBuffer_.readableBytes();
        if(oldLen + remaining >= highWaterMark_
            && oldLen < highWaterMark_
            &&highWaterMarkCallback_){
                loop_->queueInLoop(
                    [this,remaining,oldLen](){
                        highWaterMarkCallback_(shared_from_this(),oldLen + remaining);
                    }
                );
            }
            outputBuffer_.append((char*)data + nwrote,remaining);
            if(!channel_->isWritng()){
                channel_->enableWriting();
            }
    }
}


void TcpConnection::send(const std::string& buf){
    if(state_ == KConnected){
        if(loop_->isInLoopThread()){
            sendInLoop(buf.c_str(),buf.size());
        }
        else {
            loop_->runInLoop(
                [this,buf](){
                    sendInLoop(buf.c_str(),buf.size());
                }
            );
        }
    }
    else {
        LOG_ERROR("DisConnected but send,send failed!fd=%d\n",channel_->getFd());
    }
}

//建立连接
void TcpConnection::connectionEstablished(){
    setState(KConnected);
    //由于channel中的回调都是捕获了this
    //只有连接还存在的时候才能调用回调
    //tie用一个弱智能指针，观察连接是否还活着，或者才执行相应的回调
    channel_->tie(shared_from_this());
    channel_->enableReading();//向poller注册channel的epollin事件

    //新连接建立，执行回调
    connectionCallback_(shared_from_this());
}

//销毁连接
void TcpConnection::connectionDestroyed(){
    if(state_ == KConnected){
        setState(kDisconnected);
        channel_->disableAll();//把channel的所有感兴趣事件，从poller中del掉
    }
    channel_->remove();
}


void TcpConnection::shutdown(){
    if(state_ == KConnected){
        setState(kDisconnecting);
        loop_->runInLoop(
            [this](){
                shutdownInLoop();
            }
        );
    }
}

void TcpConnection::shutdownInLoop(){
    if(!channel_->isWritng()){//说明当前outputBuffer中的数据已经全部发送完成
        socket_->shutdownWrite();//关闭写端，epollhub发生 -> channel的handleclose方法
    }
}
