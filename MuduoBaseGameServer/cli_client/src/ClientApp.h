#pragma once

#include "NetworkClient.h"
#include "GameState.h"
#include "UIRenderer.h"
#include <string>
#include <vector>

namespace cabo {

class ClientApp {
public:
    void run();

private:
    NetworkClient network_;
    GameState state_;
    UIRenderer renderer_;
    bool running_ = true;
    int64_t nextSeq_ = 1;

    // 流程方法
    void connectToServer();
    void loginFlow();
    void roomFlow();
    void waitingRoomLoop();
    void gameLoop();

    // 输入处理
    void handleGameInput();
    void handleDrawnCardDecision();
    void handleReplaceWithDrawn();

    // 工具方法
    std::vector<int> parseSlotIndices(const std::string& input);
};

} // namespace cabo
