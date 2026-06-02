#pragma once
#include "noncopyable.h"
#include <unistd.h>
#include <memory>
#include <thread>
#include <atomic>
#include <string>
#include <functional>
#include <cstdint>
class Thread{//一个Thread对象就是一个线程的详细信息的集合
public:
    using ThreadFunc = std::function<void()> ;
    explicit Thread(ThreadFunc func,std::string name = std::string());
    ~Thread(); 
    void start();
    void join();

    static int getNumCreated() {return static_cast<int>(numCreated_);}
    std::string getName() const {return name_;}
    pid_t tid()const {return tid_;}
    bool started()const {return started_;}
   
private:
    void setDefaultName();
    bool started_{false};
    bool joined_{false};
    pid_t tid_;
    std::string name_;
    ThreadFunc func_;
    std::shared_ptr<std::thread> thread_;
    static std::atomic_int32_t numCreated_;
};