#include <mymuduo/TcpServer.h>
#include <mymuduo/logger.h>
#include <string>

class EchoServer{
public:
    EchoServer(EventLoop* loop,
                const InetAddress& addr, 
                const std::string& name):
    server_(loop,addr,name),
    loop_(loop)
    {
        //注册回调函数
        server_.setConnectionCallback(
            [this](const TcpConnectionPtr& conn){
                onConnection(conn);
            }
        );
        server_.setMessageCallback(
            [this](const TcpConnectionPtr& conn,Buffer* buf,Timestamp time){
                onMessage(conn,buf,time);
            }
        );
        //设置合适的loop线程数量 oneloop per thread
        server_.setThreadNum(3);
    }

    void start(){
        server_.start();
    }
private:
    void onConnection(const TcpConnectionPtr& conn){
        //连接建立或者断开的回调
        
        if(conn->connected()){
            LOG_INFO("conn UP : %s\n",conn->peerAddress().toIpPort().c_str());
        }
        else{
            LOG_INFO("Connection DOWN : %s",conn->peerAddress().toIpPort().c_str());
        }
    }

    //可读写事件回调
    void onMessage(const TcpConnectionPtr& conn,Buffer* buf,Timestamp time){
        std::string msg = buf->retrieveAllAsString();
        conn->send(msg);
        conn->shutdown();//关闭写端 EPOLLHUB=>closeCallback
        //写一次就关闭连接了
    }
    EventLoop* loop_;
    TcpServer server_;
};


int main(){
    EventLoop mainloop;
    InetAddress addr;
    EchoServer server(&mainloop,addr,"TestServer");
    server.start();
    mainloop.loop();
    return 0;
}