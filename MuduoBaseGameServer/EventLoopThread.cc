#include "EventLoopThread.h"
#include "EventLoop.h"


EventLoopThread::EventLoopThread(const ThreadInitCallback& cb,
        const std::string&name )
:loop_(nullptr)
,exiting_(false)
,thread_([this](){
    threadFunc();}
    ,name)
,mtx_()
,cond_()
,callback_(cb)
{}

EventLoopThread::~EventLoopThread()
{
    exiting_ = true;
    if(loop_ != nullptr){
        loop_ -> quit();
        thread_.join();
    }
}

EventLoop* EventLoopThread::startLoop(){
    thread_.start();

    EventLoop* loop = nullptr;
    {
        std::unique_lock<std::mutex> lock(mtx_);
        cond_.wait(lock,[this]()->bool{return loop_ != nullptr;});
        loop = loop_;
    }
    return loop;
}


//该方法是在单独的新线程里面运行的
void EventLoopThread::threadFunc(){
    EventLoop loop;//创建一个独立的eventloop,和创建的线程是一一对应的，one loop per thread在次体现
    
    if(callback_){
        callback_(&loop);
    }

    {
        std::lock_guard<std::mutex> lock(mtx_);
        loop_ = &loop;
        cond_.notify_one();
    }

    loop.loop();//启动对应的Poller的poll
    std::lock_guard<std::mutex> lock(mtx_);
    loop_ = nullptr;
}