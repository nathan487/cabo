#include "Poller.h"
#include "EPollPoller.h"
#include <stdlib.h>

Poller* Poller::newDefaultPoller(EventLoop* loop)
{
    // Always use EPollPoller as default
    return new EPollPoller(loop);
}