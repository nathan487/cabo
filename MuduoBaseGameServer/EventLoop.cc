#include "EventLoop.h"
#include <sys/eventfd.h>
#include <unistd.h>
#include <fcntl.h>
#include "logger.h"
#include <memory>


__thread EventLoop* t_loopInThisThread = nullptr;

//定义默认的Poller Io复用接口超时事件
const int kPollTimeMs = 10000;


//创建wakeupfd，用来notify唤醒shubReactor处理新来的channel
int createEventfd(){
    int evtfd = ::eventfd(0,EFD_NONBLOCK | EFD_CLOEXEC);
    if(evtfd < 0){
        LOG_FATAL("eventfd error:%d\n",errno)
    }
    return evtfd;
}

EventLoop::EventLoop()
:looping_(false)
,quit_(false)
,callingPendingFunctors_(false)
,threadId_(CurrentThread::tid())
,Poller_(Poller::newDefaultPoller(this))
,wakeupFd_(createEventfd())
,wakeupChannel_(new Channel(this,wakeupFd_))
,currentActiveChannel_(nullptr)
{
    LOG_DEBUG("Event created %p in thread %d\n",this,threadId_);
    if(t_loopInThisThread){
        LOG_FATAL("Another EventLoop %p exists in this thread %d \n",t_loopInThisThread,threadId_);

    }
    else {
        t_loopInThisThread  = this;
    }

    //设置wakeupfd的事件类型以及发生事件后的回调操作
    wakeupChannel_->setReadCallback([this](Timestamp ){
            handleRead();
    });
    //每一个eventloop都将监听wakeupchannel的EpollIN读事件了
    wakeupChannel_->enableReading();
}
EventLoop::~EventLoop(){
    wakeupChannel_->disableAll();
    wakeupChannel_->remove();
    ::close(wakeupFd_);
    t_loopInThisThread = nullptr;

}

void EventLoop::handleRead(){
    uint64_t one = 1;
    ssize_t n = read(wakeupFd_,&one,sizeof one);
    if(n != sizeof one){
        LOG_ERROR("EventLoop::handleRead() reads %lu bytes instead of 8\n",n);

    }
}


void EventLoop::loop(){
    looping_ = true;
    quit_ = false;

    LOG_INFO("EventLoop %p start looping\n",this);

    while (!quit_)
    {
        activeChannels_.clear();
        pollReturnTime_ = Poller_->poll(kPollTimeMs,&activeChannels_);
        for(Channel* Channel : activeChannels_){
            //poller监听那些channel发生事件
            Channel->handleEvent(pollReturnTime_);
        }
        //mainloop实现注册一个回调cb(需要subloop来执行) wakeup subloop之后，执行下面的方法，subloop就来执行之前mainloop注册的方法

        doPendingFunctors();
    }

    LOG_INFO("EventLoop %p stop looping. \n",this);
    looping_ = false;
    
    
}


void EventLoop::quit(){
    quit_ = true;
    //如果是subloop调用mainloop的quit函数，就需要将其唤醒以停止looping
    if(!isInLoopThread()){
        wakeup();
    }
}

//在当前loop中执行cb
void EventLoop::runInLoop(Functor cb){
    if(isInLoopThread()){
        cb();
    }
    else {
        queueInLoop(cb);
    }
}
    //cb放入队列中，唤醒loop所在的线程，执行cb
void EventLoop::queueInLoop(Functor cb){
    {
        std::unique_lock<std::mutex> lock(mtx_);
        pendingFunctors_.emplace_back(cb);
    }

    //唤醒相应的，需要执行上面回调操作的loop的线程
    //或者当前loop已经在执行回调了，但是loop又有了新的回调
    if(!isInLoopThread() || callingPendingFunctors_){
        wakeup();//唤醒loop所在线程
    }
}


void EventLoop::wakeup(){
    uint64_t one = 1;
    ssize_t n = write(wakeupFd_,&one,sizeof one);//发生事件，poller被唤醒
    if(n != sizeof one){
        LOG_ERROR("EventLoop::wakeup() writes %lu bytes instead of 8",n);

    }
}

void EventLoop::updateChannel(Channel* channel){
    Poller_->updateChannel(channel);
}
void EventLoop::removeChannel(Channel* channel){
    Poller_->removeChannel(channel);
}
bool EventLoop::hasChannel(Channel* channel){
    return Poller_->hasChannel(channel);
}
void EventLoop::doPendingFunctors(){
    std::vector<Functor> functors;
    callingPendingFunctors_ = true;

    {
        std::lock_guard<std::mutex> lock(mtx_);
        functors.swap(pendingFunctors_);
    }

    for(const Functor& functor:functors){
        functor();//执行当前loop需要执行的回调操作
    }
    callingPendingFunctors_ = false;
}//执行回调
