#include "Acceptor.h"
#include <sys/types.h>          /* See NOTES */
#include <sys/socket.h>
#include "logger.h"
#include <unistd.h>
/**
 * acceptor主要是对acceptChannel 和 acceptSocket listen()和accept()进行封装
 * accept写在handleRead中，listenfd上面发生的读事件就是新连接到来
 * accept新连接之后，调用newConnectionCallback(TcpServer进行设置的)
 * 
 */
//static避免重定义
static int createNonblocking(){
    int sockfd = ::socket(AF_INET,SOCK_STREAM | SOCK_NONBLOCK | SOCK_CLOEXEC,IPPROTO_TCP);
    if(sockfd < 0){
        LOG_FATAL("%s:%s:%d listen socket create err:%d",__FILE__,__FUNCTION__,__LINE__,errno);
    }
    return sockfd;
}

Acceptor::Acceptor(EventLoop* loop,const InetAddress& listenAddr,bool reuseport)
    :loop_(loop)
    ,acceptSocket_(createNonblocking())
    ,acceptChannel_(loop,acceptSocket_.fd())
    ,listenning_(false)
{
    acceptSocket_.setReuseAddr(true);
    acceptSocket_.setReusePort(reuseport);
    acceptSocket_.bindAddress(listenAddr);
    acceptChannel_.setReadCallback(
        [this](Timestamp){handleRead();}
    );
}

Acceptor::~Acceptor(){
    acceptChannel_.disableAll();
    acceptChannel_.remove();

}




void Acceptor::listen(){
    listenning_ = true;
    acceptSocket_.listen();
    acceptChannel_.enableReading();

}


void Acceptor::handleRead(){
    InetAddress peerAddr;
    int connfd = acceptSocket_.accept(&peerAddr);
    if(connfd >= 0){
        if(newConnectionCallback_){
            newConnectionCallback_(connfd,peerAddr);//这个callback执行轮询找到subloop，唤醒
            
        }
        else {
            ::close(connfd);
        }
    }
    else {
        LOG_ERROR("%s:%s:%d accept  err:%d",__FILE__,__FUNCTION__,__LINE__,errno);
        if(errno == EMFILE){
            LOG_ERROR("%s:%s:%d sockfd reached limit!\n",__FILE__,__FUNCTION__,__LINE__);
        }
    }
}
