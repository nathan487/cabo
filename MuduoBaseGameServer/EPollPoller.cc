#include "EPollPoller.h"
#include "logger.h"
#include <error.h>
#include <unistd.h>
#include <iostream>
#include "Channel.h"
#include <string.h>


//channel未添加到poller中
constexpr int kNew = -1;
//channel已经添加到poller中
constexpr int kAdded = 1;
//channel从poller中删除
constexpr int kDeleted = 2;

EPollPoller::EPollPoller(EventLoop* loop)
:Poller(loop)
,epollfd_(::epoll_create1(EPOLL_CLOEXEC))
,events_(kInitEventListsSize)
{
    if(epollfd_ < 0){
        LOG_FATAL("epoll_create error:%d\n",errno)
    }
}

EPollPoller::~EPollPoller() {
    ::close(epollfd_);
}

//重写基类抽象方法



//
void EPollPoller::updateChannel(Channel* channel){
    const int index = channel->getIndex();
    LOG_INFO("func=%s => fd=%d events=%d index=%d\n",__FUNCTION__,channel->getFd(),channel->getEvents(),channel->getIndex());

    if(index == kNew || index == kDeleted){
        if(index == kNew){
            int fd = channel->getFd();
            channels_[fd] = channel;
        }
        channel->setIndex(kAdded);
        update(EPOLL_CTL_ADD,channel);
    }
    else {
        int fd = channel->getFd();
        if(channel->isNoneEvent()){
            update(EPOLL_CTL_DEL,channel);
            channel->setIndex(kDeleted);
        }
        else {
            update(EPOLL_CTL_MOD,channel);
        }
    }
}

void EPollPoller::removeChannel(Channel* channel){
    int fd = channel->getFd();
    channels_.erase(fd);
    LOG_INFO("func=%s => fd=%d events=%d index=%d\n",__FUNCTION__,channel->getFd(),channel->getEvents(),channel->getIndex());

    int index = channel->getIndex();
    if(index == kAdded){
        update(EPOLL_CTL_DEL,channel);
    }
    channel->setIndex(kNew);
}


//根据对应的operation
void EPollPoller::update(int operation,Channel* channel){
    epoll_event event;
    memset(&event,0,sizeof event);
    event.events = channel->getEvents();
    event.data.ptr = channel;
    int fd = channel->getFd();

    if(::epoll_ctl(epollfd_,operation,fd,&event) < 0){
        if(operation == EPOLL_CTL_DEL){
            LOG_ERROR("epoll_ctl_del error:%d\n",errno);

        }
        else {
            LOG_FATAL("epoll_ctl add/mod error:%d\n",errno);
        }
    }
}


//调用epoll_wait，已就绪的事件响应的fd就会在events中，再调用fillActivateChannels
Timestamp EPollPoller::poll(int timeoutMs,ChannelList* activeChannels){
    // LOG_INFO("func=%s => fd totalcount:%lu\n",__FUNCTION__,channels_.size());
    int numEvents = ::epoll_wait(epollfd_,
                                &*events_.begin(),
                                static_cast<int>(events_.size()),
                                timeoutMs);
    int savedError = errno;
    Timestamp now(Timestamp::now());
    if(numEvents > 0){
        fillActiveChannels(numEvents,activeChannels);
        //如果已就绪的fd数量与events容量相等，说明可以适当增加容量
        if(static_cast<size_t>(numEvents) == events_.size()){
            events_.resize(events_.size() * 2);
        }
    }
    else if(numEvents == 0){
        //没有事情可做，但是缩小的话会影响还在筹备中的fd,所以events只增不缩
        LOG_DEBUG("%s timeout!\n",__FUNCTION__);
    }
    else {
        if(savedError != EINTR){
            errno = savedError;
            LOG_ERROR("EPollPoller::poll() err!");
        }
    }
    return now;                

}

//从events中取出已就绪事件的fd,然后通过map找到对应的channel指针，调用setRevent方法设置其revents再通过channel调用
//注册好的回调函数
void EPollPoller::fillActiveChannels(int numEvents,ChannelList* activeChannels)const {
    for(int i = 0;i < numEvents;++i){
        Channel* channel = static_cast<Channel*>(events_[i].data.ptr);

        int fd = channel->getFd();
        auto it = channels_.find(fd);
        if(it != channels_.end() && it->second == channel){
            channel->set_revents(events_[i].events);
            activeChannels->push_back(channel);//EventLoop就拿到了Poller给它返回的所有事件的channel列表了
        }
    }
}
    

