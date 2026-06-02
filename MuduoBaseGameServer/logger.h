#pragma once
#include "noncopyable.h"
#include <string>

#define LOG_INFO(LogmsgFormat , ...) \
    do { \
        Logger& logger = Logger::instance();\
        char buf[1024] = {0};\
        snprintf(buf,1024,LogmsgFormat,##__VA_ARGS__);\
        logger.log(INFO, buf);\
    }while(0);

#define LOG_ERROR(LogmsgFormat , ...) \
    do { \
        Logger& logger = Logger::instance();\
        char buf[1024] = {0};\
        snprintf(buf,1024,LogmsgFormat,##__VA_ARGS__);\
        logger.log(ERROR, buf);\
    }while(0);

#define LOG_FATAL(LogmsgFormat , ...) \
    do { \
        Logger& logger = Logger::instance();\
        char buf[1024] = {0};\
        snprintf(buf,1024,LogmsgFormat,##__VA_ARGS__);\
        logger.log(FATAL, buf);\
        exit(-1);\
    }while(0);

#ifdef MUDEBUG
#define LOG_DEBUG(LogmsgFormat , ...) \
    do { \
        Logger& logger = Logger::instance();\
        char buf[1024] = {0};\
        snprintf(buf,1024,LogmsgFormat,##__VA_ARGS__);\
        logger.log(DEBUG, buf);\
    }while(0);
#else 
    #define LOG_DEBUG(LogmsgFormat , ...) 
#endif

enum LogLevel{
    INFO, //普通信息
    ERROR ,//错误信息
    FATAL,//core信息
    DEBUG,//调试信息
};

class Logger: noncopyable{
public :
    static Logger& instance();

    void setLogLevel(int level);

    void log(int level, const std::string& msg);
private :
    int logLevel_;
    
};