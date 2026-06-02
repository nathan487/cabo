#include "TcpServer.h"
#include "logger.h"
#include <functional>
#include <strings.h>
#include "TcpConnection.h"

static EventLoop* CheckLoopNotNull(EventLoop* loop){
    if(loop == nullptr){
        LOG_FATAL("%s:%s:%d mainLoop is null!\n",__FILE__,__FUNCTION__,__LINE__);
    }
    return loop;
}



//newConnectionCallback就是用来执行接收fd上面的数据之后，轮询分配给某个subloop去执行的
TcpServer::TcpServer(EventLoop* loop,
            const InetAddress& listenAddr,
            const std::string& nameArg,
            Option option)
            : loop_(CheckLoopNotNull(loop))
            ,ipPort_(listenAddr.toIpPort())
            ,name_(nameArg)
            ,acceptor_(new Acceptor(loop,listenAddr,option == kReusePort))
            ,threadPool_(new EventLoopThreadPool(loop,name_))
            ,connectionCallback_()
            ,messageCallback_()
            ,nextConnId_(1)
            ,started_(0)
{
    acceptor_->setNewConnectionCallback(
        [this](int sockfd,const InetAddress& peerAddr){
            //传入连接fd和对方的ip地址与端口号
            newConnection(sockfd,peerAddr);
        }
    );
}

TcpServer::~TcpServer(){
    for(auto& item : connections_){
        TcpConnectionPtr conn(item.second);
        item.second.reset();

        conn->getLoop()->runInLoop(
            [conn](){//值拷贝,loop执行完之后这个对象就彻底销毁
                conn->connectionDestroyed();
            }
        );
    }
}

void TcpServer::setThreadNum(int numThread){
    threadPool_->setThreadNum(numThread);
}

    //开启tcp服务器，开始监听
    //TcpServer.start => mainloop.loop => mainpoller.poll => listen_epoll.epoll_wait

void TcpServer::start(){
    if(started_++ == 0){
        threadPool_->start(threadInitCallback_);
        loop_->runInLoop(
            [this](){
                acceptor_->listen();
            }
        );
        LOG_INFO("TcpServer start at %s\n",this->ipPort_.c_str());
    }
}

//有一个新客户端的连接，Acceptor会执行这个回调
void TcpServer::newConnection(int sockfd,const InetAddress& peerAddr){
    EventLoop* ioLoop = threadPool_->getNextLoop();
    char buf[64] = {0};
    snprintf(buf,sizeof buf,"-%s#%d",ipPort_.c_str(),nextConnId_++);
    
    std::string connName = name_ + buf;

    LOG_INFO("TcpServer::newConnection [%s] - new connection [%s] from %s",
    name_.c_str(),connName.c_str(),peerAddr.toIpPort().c_str());
    
    //通过sockfd获取其绑定的本机ip地址和端口信息
    sockaddr_in local; 
    ::bzero(&local,sizeof local);
    socklen_t addrlen = sizeof local;
    if(::getsockname(sockfd,(sockaddr*)&local,&addrlen) < 0){
        LOG_ERROR("sockets::getLocalAddr\n");
    }
    InetAddress localAddr(local);

    //根据成功连接的sockfd,创建tcpConnection连接对象
    TcpConnectionPtr conn(new TcpConnection(ioLoop,
                                            connName,
                                            sockfd,
                                            localAddr,
                                            peerAddr));
    connections_[connName] = conn;
    
    //以下回调是用户设置给TcpServer=>TcpConnection=>Channel=>Poller=>notify channel调用回调
    conn->setConnectionCallback(connectionCallback_);
    conn->setMessageCallback(messageCallback_);
    conn->setWriteCompleteCallback(writeCompleteCallback_);

    //设置了 如何关闭连接的回调
    conn->setCloseCallback(
        [this](TcpConnectionPtr connectionToClose){
            removeConnction(connectionToClose);
        }
    );
    
    ioLoop->runInLoop(
        [conn](){
            conn->connectionEstablished();
        }
    );
}

void TcpServer::removeConnctionInLoop(const TcpConnectionPtr& conn){
    LOG_INFO("TcpServer::removeConnectionInLoop [%s] - connection %s\n",
        name_.c_str(),conn->name().c_str());

    connections_.erase(conn->name());
    EventLoop* ioLoop = conn->getLoop();
    ioLoop->queueInLoop(
        [conn](){
            conn->connectionDestroyed();
        }
    );
}

void TcpServer::removeConnction(const TcpConnectionPtr& conn){//设置给Connction
    loop_->runInLoop(
        [this,conn](){
            removeConnctionInLoop(conn);
        }
    );
}
