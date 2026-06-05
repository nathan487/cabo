#include <iostream>
#include "GameState.h"
#include "NetworkClient.h"

int main(int argc, char* argv[]) {
    std::cout << "Cabo CLI Client v1.0" << std::endl;

    // 测试NetworkClient
    cabo::NetworkClient network;

    std::cout << "Testing connection to 127.0.0.1:8888..." << std::endl;
    if (network.connect("127.0.0.1", 8888)) {
        std::cout << "Connection test successful!" << std::endl;
        network.disconnect();
    } else {
        std::cout << "Connection test failed (server not running?)" << std::endl;
    }

    return 0;
}
