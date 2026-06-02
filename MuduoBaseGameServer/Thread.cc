#include "Thread.h"
#include "CurrentThread.h"
#include <condition_variable>


std::atomic_int32_t Thread::numCreated_{0};
Thread::Thread(ThreadFunc func,std::string name )
:func_(std::move(func))
,name_(name)
,started_(false)
,joined_(false)
,tid_(0)
{
    setDefaultName();
}

Thread::~Thread(){
    if(started_ &&  !joined_){
        thread_->detach();
    }

}

void Thread::start(){
    started_ = true;
    std::mutex mtx;
    std::condition_variable cond;
    bool ready = false;
    
    thread_ = std::make_shared<std::thread>(
        [&](){
            tid_ = CurrentThread::tid();
            {
                std::lock_guard<std::mutex> lock(mtx);
                ready = true;
            }
            cond.notify_one();
            func_();
        }
    );

    //这里必须等待获取上面新创建的线程的tid
    {
        std::unique_lock<std::mutex> lock(mtx);
        cond.wait(lock, [&]{ return ready; });
    }
}

void Thread::join(){
    joined_ = true;
    if(thread_->joinable())thread_->join();

}

void Thread::setDefaultName(){
    int num = ++numCreated_;
    if(name_.empty()){
        char buf[32] = {0};
        snprintf(buf,sizeof buf,"thread%d",num);
        name_ = buf;
    }


}
