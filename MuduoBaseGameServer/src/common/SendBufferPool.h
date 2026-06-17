#pragma once

#include <cstddef>
#include <string>
#include <utility>
#include <vector>

namespace game {

class SendBufferPool {
public:
    class Lease {
    public:
        Lease() = default;

        Lease(SendBufferPool* pool, std::string&& buffer)
            : pool_(pool), buffer_(std::move(buffer)) {}

        ~Lease() {
            release();
        }

        Lease(const Lease&) = delete;
        Lease& operator=(const Lease&) = delete;

        Lease(Lease&& other) noexcept
            : pool_(other.pool_), buffer_(std::move(other.buffer_)) {
            other.pool_ = nullptr;
        }

        Lease& operator=(Lease&& other) noexcept {
            if (this != &other) {
                release();
                pool_ = other.pool_;
                buffer_ = std::move(other.buffer_);
                other.pool_ = nullptr;
            }
            return *this;
        }

        std::string& get() { return buffer_; }
        const std::string& get() const { return buffer_; }

    private:
        void release() noexcept {
            if (!pool_) return;
            pool_->release(std::move(buffer_));
            pool_ = nullptr;
        }

        SendBufferPool* pool_ = nullptr;
        std::string buffer_;
    };

    explicit SendBufferPool(std::size_t maxCachedBuffers = 32,
                            std::size_t maxRetainedCapacity = 64 * 1024)
        : maxCachedBuffers_(maxCachedBuffers),
          maxRetainedCapacity_(maxRetainedCapacity) {}

    Lease acquire() {
        std::string buffer;
        if (!buffers_.empty()) {
            buffer = std::move(buffers_.back());
            buffers_.pop_back();
            buffer.clear();
        }
        return Lease(this, std::move(buffer));
    }

    std::size_t cachedBufferCount() const {
        return buffers_.size();
    }

    static SendBufferPool& threadLocal() {
        thread_local SendBufferPool pool;
        return pool;
    }

private:
    void release(std::string&& buffer) noexcept {
        if (buffers_.size() >= maxCachedBuffers_
            || buffer.capacity() > maxRetainedCapacity_) {
            return;
        }

        buffer.clear();
        try {
            buffers_.push_back(std::move(buffer));
        } catch (...) {
        }
    }

    std::size_t maxCachedBuffers_;
    std::size_t maxRetainedCapacity_;
    std::vector<std::string> buffers_;
};

} // namespace game
