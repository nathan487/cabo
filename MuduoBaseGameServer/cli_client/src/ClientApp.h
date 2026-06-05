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

    // Constants
    static constexpr int MAX_SLOT_INDEX = 10;  // 最大槽位数（失败替换可能超过4）
    static constexpr int SKILL_TYPE_NONE = 1;

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
    void handleTakeFromDiscard();
    void handleSkillInput(int skillType);
    void handlePeekSelfSkill();
    void handleSpySkill();
    void handleSwapSkill();

    // 错误处理
    void handleServerError(const game::messages::ServerMessage& msg);
    bool getIntInput(int& out, int min, int max);

    // 工具方法
    std::vector<int> parseSlotIndices(const std::string& input);
};

} // namespace cabo
