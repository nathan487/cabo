#pragma once
#include "noncopyable.h"
#include "EventLoop.h"
#include "Acceptor.h"
#include "InetAddress.h"
#include <functional>
#include <memory>
#include "EventLoopThreadPool.h"
#include "Callbacks.h"
#include <atomic>
#include <unordered_map>
#include "TcpConnection.h"
#include "Buffer.h"


/**
ConnectionCallback connectionCallback_;//有新连接时的回调在ConnectionEstablished中调用
MessageCallback messageCallback_;//有消息的时候的回调
WriteCompleteCallback writeCompleteCallback_;//消息发送完以后的回调
这些是用户设置的回调，在调用newConnection的时候设置在TcpConnection中

 */
class TcpServer : noncopyable {
public:
    using ThreadInitCallback = std::function<void(EventLoop*)>;

    enum Option{
        //是否复用端口
        kNoReusePort,
        kReusePort,
    };

    TcpServer(EventLoop* loop,
            const InetAddress& listenAddr,
            const std::string& nameArg,
            Option option = kNoReusePort);
    ~TcpServer();

    void setThreadInitCallback(const ThreadInitCallback& cb){threadInitCallback_ = cb;}
    void setConnectionCallback(const ConnectionCallback& cb){connectionCallback_ = cb;}
    void setMessageCallback(const MessageCallback& cb){messageCallback_ = cb;}
    void setWriteCompleteCallback(const WriteCompleteCallback& cb){writeCompleteCallback_ = cb;}

    //设置subloop的个数
    void setThreadNum(int numThread);

    //开启tcp服务器，开始监听
    //TcpServer.start => mainloop.loop => mainpoller.poll => listen_epoll.epoll_wait

    void start();
private :
    void newConnection(int sockfd,const InetAddress& peerAddr);
    void removeConnction(const TcpConnectionPtr& conn);
    void removeConnctionInLoop(const TcpConnectionPtr& conn);
    
    using ConnectionMap = std::unordered_map<std::string,TcpConnectionPtr> ;

    EventLoop* loop_;//baseloop;

    const std::string ipPort_;
    const std::string name_;

    std::unique_ptr<Acceptor> acceptor_;//运行在mainloop,任务是监听新连接事件

    std::shared_ptr<EventLoopThreadPool> threadPool_;//one loop per thread;


    ConnectionCallback connectionCallback_;//有新连接时的回调
    MessageCallback messageCallback_;//有消息的时候的回调
    WriteCompleteCallback writeCompleteCallback_;//消息发送完以后的回调

    ThreadInitCallback threadInitCallback_;//loop线程初始化的回调

    std::atomic_int started_;

    int nextConnId_;
    ConnectionMap connections_;//用哈希表保存所有的连接
};