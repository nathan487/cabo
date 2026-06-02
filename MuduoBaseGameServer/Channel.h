#pragma once
#include "noncopyable.h"
#include <functional>
#include "Timestamp.h"
#include <memory>
class EventLoop;

class Channel:noncopyable{
public:
    using EventCallback = std::function<void()>;
    using ReadEventCallback = std::function<void(Timestamp)>;


    Channel(EventLoop* loop,int fd);
    ~Channel();
    //fd得到poller通知的revents之后，处理事件的
    void handleEvent(Timestamp receiveTime);

    //设置回调函数对象
    void setReadCallback(ReadEventCallback cb){
        readCallback_  = std::move(cb);
    }
    void setWriteCallback(EventCallback cb){
        writeCallback_ = std::move(cb);
    }
    void setCloseCallback(EventCallback cb){
        closeCallBack_ = std::move(cb);
    }
    void setErrorCallback(EventCallback cb){
        errorCallback_ = std::move(cb);
    }

    //防止当channel被手动remove叼，channel还在执行回调操作
    void tie(const std::shared_ptr<void>&);//观察者语义

    int getFd() const {return fd_;}
    int getEvents() const {return events_;}
    void set_revents(int revt){revents_ = revt;}
    bool isNoneEvent() const {return events_ == kNoneEvent;}

    void enableReading() {events_ |= kReadEvent;update();}
    void disableReading() {events_ &= ~kReadEvent;update();}
    void enableWriting() {events_ |= kWriteEvent;update();}
    void disableWriting() {events_ &= ~kWriteEvent;update();}
    void disableAll() {events_ = kNoneEvent;update();}

    //返回fd当前的事件状态
    bool isWritng() const {return events_ & kWriteEvent;}
    bool isReading() const {return events_ & kReadEvent;}

    int getIndex()const {return index_;}
    void setIndex(int idx){this->index_ = idx;}

    //one loop per thread
    EventLoop* ownerLoop() {return loop_;}
    void remove();

private:

    void update();
    void handleEventWithGuard(Timestamp receiveTime);

    static const int kNoneEvent;
    static const int kReadEvent;
    static const int kWriteEvent;

    EventLoop *loop_;
    const int fd_;
    int events_;
    int revents_;
    int index_;

    std::weak_ptr<void> tie_;
    bool tied_;


    // 因为channel通道里面能够获知fd最终发生的事件revents,
    //所以它负责调用具体事件的回调操作
    ReadEventCallback readCallback_;
    EventCallback writeCallback_;
    EventCallback closeCallBack_;
    EventCallback errorCallback_;
};