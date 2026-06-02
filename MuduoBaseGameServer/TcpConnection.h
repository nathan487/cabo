#pragma once

#include "noncopyable.h"
#include <memory>
#include <string>
#include <atomic>
#include "InetAddress.h"
#include "Callbacks.h"
#include "Buffer.h"
#include "Timestamp.h"


class Channel;
class EventLoop;
class Socket;


/*
TcpServr => Accep
*/
class TcpConnection : noncopyable ,public std::enable_shared_from_this<TcpConnection>
{
public:
    
    TcpConnection(EventLoop* loop,
        const std::string& name,
        int sockfd,
        const InetAddress& localAddr,
        const InetAddress& peerAddr);

    ~TcpConnection();

    EventLoop* getLoop() const {return loop_;}
    const std::string& name() const {return name_;}
    const InetAddress& localAddr() const {return localAddr_;}
    const InetAddress& peerAddress() const {return peerAddr_;}


    bool connected() const {return state_ == KConnected;}

    //发送数据
    void send(const std::string& buf);
    //关闭连接
    void shutdown();


    void setConnectionCallback(const ConnectionCallback& cb){connectionCallback_ = cb;}
    void setMessageCallback(const MessageCallback& cb){messageCallback_ = cb;}
    void setWriteCompleteCallback(const WriteCompleteCallback& cb){writeCompleteCallback_ = cb;}
    void setHighWaterMarkCallback(const HighWaterMarkCallback& cb){ highWaterMarkCallback_= cb;}
    void setCloseCallback(const CloseCallback& cb){closeCallback_ = cb;}


    //建立连接
    void connectionEstablished();
    //销毁连接
    void connectionDestroyed();
private:
    enum StateE{
        kDisconnected,
        kConnecting,
        KConnected,
        kDisconnecting
    };
    void setState(StateE state){state_ = state;}

    void handleRead(Timestamp receiveTime);
    void handleWrite();
    void handleClose();
    void handleError();

    void sendInLoop(const void* message,std::size_t len);
    void shutdownInLoop();

    EventLoop* loop_;//这里绝对不是baseloop,因为TcpConnection都是在subLoop里面管理的
    const std::string name_;
    std::atomic_int state_;
    bool reading_;

    //Acceptor是为mainLoop设计的， TcpConnection是为subLoop设计的
    std::unique_ptr<Socket> socket_;
    std::unique_ptr<Channel> channel_;

    const InetAddress localAddr_;
    const InetAddress peerAddr_;

    //用户在tcpServer上面设置回调=> tcpServer丢给TcpConnection =>tcpConnection丢给Channel 
    //eventloop 调用 poller 监听事件 
    //事件发生
    //eventloop 得到poller返回的activeChannels
    //调用channel->handleEvents -> 执行用户设置的回调
    ConnectionCallback  connectionCallback_;
    MessageCallback messageCallback_;
    WriteCompleteCallback writeCompleteCallback_;
    HighWaterMarkCallback highWaterMarkCallback_;
    CloseCallback closeCallback_;

    std::size_t highWaterMark_;

    Buffer inputBuffer_;//接收数据的缓冲区
    Buffer outputBuffer_;//发送数据的缓冲区
};

