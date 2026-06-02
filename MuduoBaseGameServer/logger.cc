#include "logger.h"
#include <iostream>
#include "Timestamp.h"
 Logger& Logger::instance(){
    static Logger logger;
    return logger;
 }

void Logger::setLogLevel(int level){
    this->logLevel_ = level;

}

void Logger::log(int level, const std::string& msg){
    switch (level)
    {
    case INFO:
        std::cout<<"[INFO]";
        break;
    case ERROR:
        std::cout<<"[ERROR]";
        break;
    case DEBUG:
        std::cout<<"[DEBUG]";
        break;
    case FATAL:
        std::cout<<"[FATAL]";
        break;
    default:
        break;
    }

    std::cout << Timestamp::now().toString() <<' ' << msg <<std::endl;
}



