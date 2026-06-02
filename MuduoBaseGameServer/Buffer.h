#pragma once
#include <vector>
#include <string>
#include <algorithm>


class Buffer{

public :
    static const std::size_t kCheapPrepend = 8;
    static const std::size_t kInitialSize = 1024;

    explicit Buffer(std::size_t initialSize = kInitialSize)
    :buffer_(kCheapPrepend + initialSize)
    ,readerIndex_(kCheapPrepend)
    ,writerIndex_(kCheapPrepend)
{}
    std::size_t readableBytes()const{
        return writerIndex_ - readerIndex_;
    }

    std::size_t writableBytes()const {
        return buffer_.size() - writerIndex_;
    }

    std::size_t prependableBytes() const {
        return readerIndex_ ;
    }

    //返回可读数据缓冲区的起始地址
    const char* peek()const{
        return begin() + readerIndex_;
    }

    //onMessage string <- buffer
    void retrieve(std::size_t len){
        if(len < readableBytes()){
            readerIndex_ += len;
        }
        else {
            retrieveAll();
        }
    }

    void retrieveAll(){
        readerIndex_ = writerIndex_ = kCheapPrepend;
    }
    
    std::string retrieveAllAsString(){
        return retrieveAsString(readableBytes());//应用可读取数据的长度
    }

    std::string retrieveAsString(std::size_t len){
        std::string result(peek(),len);
        retrieve(len);//上面一句把缓冲区中可读的数据，已经读取出来。这里进行复位操作
        return result;
    }

    void ensureWritableBytes(std::size_t len){
        if(writableBytes() < len){
            makeSpace(len);//扩容函数
        }

    }

    void append(const char* data, std::size_t len){
        ensureWritableBytes(len);
        std::copy(data,data + len,beginWrite());
        writerIndex_ += len;
    }

    //从fd上读取数据
    ssize_t readFd(int fd,int* saveErrno);
    //通过fd发送数据
    ssize_t writeFd(int fd,int* saveErrno);
private:
    void makeSpace(std::size_t len){
        if(writableBytes() + prependableBytes() < len + kCheapPrepend){
            buffer_.resize(writerIndex_ + len);
        }
        else {
            std::size_t readable = readableBytes();
            std::copy(begin() + readerIndex_,
            begin() + writerIndex_,
            begin() + kCheapPrepend );
            readerIndex_ = kCheapPrepend;
            writerIndex_ = readerIndex_ + readable;
        }
    }

    

    char* begin(){
        //就是vector底层数组首元素的地址，也就是数组的起始地址
        return &*buffer_.begin();
    }
    const char* begin() const 
    {
        return &*buffer_.begin();
    }

    char* beginWrite(){
        return begin() + writerIndex_;
    }
    const char* beginWrite()const {
        return begin() + writerIndex_;
    }
    std::vector<char> buffer_;
    std::size_t readerIndex_;
    std::size_t writerIndex_;
};