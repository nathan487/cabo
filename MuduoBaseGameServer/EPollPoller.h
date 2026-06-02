#pragma once

#include "Poller.h"
#include <vector>
#include <sys/epoll.h>

class Channel;
class EPollPoller : public Poller{
public:
    EPollPoller(EventLoop* loop);
    ~EPollPoller() override;

    //重写基类抽象方法
    Timestamp poll(int timeoutMs,ChannelList* activeChannels)override ;//epoll_wait
    void updateChannel(Channel* channel)override;//epoll_ctl
    void removeChannel(Channel* channel)override ;

private:
    using EventList = std::vector<epoll_event> ;

    static const int kInitEventListsSize = 16;

    //填写活跃的连接
    void fillActiveChannels(int numEvents,ChannelList* activeChannels)const;
    //更新channel通道
    void update(int operation,Channel* channel);

    int epollfd_;
    EventList events_;
    
};



/*
    int b1
    int b2
    int b3


*/