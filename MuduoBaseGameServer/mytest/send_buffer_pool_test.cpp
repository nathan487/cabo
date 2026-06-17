#include "common/SendBufferPool.h"

#include <cstdlib>
#include <iostream>
#include <string>

namespace {

void require(bool condition, const std::string& message) {
    if (!condition) {
        std::cerr << "FAILED: " << message << "\n";
        std::exit(1);
    }
}

void reusesReleasedStringCapacity() {
    game::SendBufferPool pool(2, 1024);

    size_t firstCapacity = 0;
    {
        auto lease = pool.acquire();
        lease.get().reserve(256);
        lease.get().assign(128, 'x');
        firstCapacity = lease.get().capacity();
        require(pool.cachedBufferCount() == 0,
                "checked-out buffer should not count as cached");
    }

    require(pool.cachedBufferCount() == 1,
            "released buffer should return to the pool");

    {
        auto lease = pool.acquire();
        require(lease.get().empty(),
                "reused buffer should be cleared before checkout");
        require(lease.get().capacity() >= firstCapacity,
                "reused buffer should keep the previous allocation capacity");
    }
}

void dropsOversizedBuffers() {
    game::SendBufferPool pool(2, 64);

    {
        auto lease = pool.acquire();
        lease.get().reserve(1024);
        lease.get().assign(512, 'x');
    }

    require(pool.cachedBufferCount() == 0,
            "oversized temporary buffers should not stay cached");
}

} // namespace

int main() {
    reusesReleasedStringCapacity();
    dropsOversizedBuffers();
    std::cout << "send_buffer_pool_test passed\n";
    return 0;
}
