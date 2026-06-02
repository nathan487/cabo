#pragma once

#include <arpa/inet.h>
#include <string>
#include <iostream>
class InetAddress{
public:
    explicit InetAddress(uint64_t port = 9090,std::string ip = "127.0.0.1");
    explicit InetAddress(const sockaddr_in& addr):addr_(addr){}

    std::string toIp() const;
    std::string toIpPort() const;
    uint16_t  toPort() const ;


    
    const sockaddr* getSockAddr() const {return reinterpret_cast<const sockaddr*>(&addr_);}
    void setSockAddr(const sockaddr_in& addr){addr_ = addr;};
private:
    sockaddr_in addr_;
};