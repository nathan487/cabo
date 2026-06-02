#pragma once 
#include "noncopyable.h"
#include "Poller.h"
#include "Channel.h"
#include <vector>
#include <atomic>
#include "Timestamp.h"
#include <memory>
#include <mutex>
#include "CurrentThread.h"
class EventLoop: noncopyable{
public:

    using Functor = std::function<void()> ;

    EventLoop();
    ~EventLoop();

    //开启事件循环
    void loop();
    //退出事件循环
    void quit();

    Timestamp pollReturnTime() const {return pollReturnTime_;}

    //在当前loop中执行cb
    void runInLoop(Functor cb);
    //cb放入队列中，唤醒loop所在的线程，执行cb
    void queueInLoop(Functor cb);

    //用来唤醒loop所在的线程
    void wakeup();

    //调用poller中的相应方法
    void updateChannel(Channel* channel);
    void removeChannel(Channel* channel);
    bool hasChannel(Channel* channel);

    //判断EventLoop对象是否在自己的线程里面
    bool isInLoopThread() const {return this->threadId_ == CurrentThread::tid();}

private:
    void handleRead();//wake up
    void doPendingFunctors();//执行回调

    using ChannelList = std::vector<Channel*>;
    std::atomic_bool looping_;//原子操作，底层通过CAS实现
    std::atomic_bool quit_;//标识是否退出loop循环
    const pid_t threadId_;//记录当前loop所在线程的pid
    Timestamp pollReturnTime_;//poller返回发生事件的channels的事件点
    std::unique_ptr<Poller> Poller_;


    int wakeupFd_; //主要作用，当mainLoop获取一个新用户的channel，通过轮询算法选择一个subloop,通过该成员唤醒subloop处理这个channel
    std::unique_ptr<Channel> wakeupChannel_;


    ChannelList activeChannels_;
    Channel* currentActiveChannel_;

    std::atomic_bool callingPendingFunctors_;//表示当前loop是否有需要执行的回调操作
    std::vector<Functor> pendingFunctors_;//loop需要执行的所有的回调操作
    std::mutex mtx_;//用来保护上面vector容器线程安全操作
};