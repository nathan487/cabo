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

    // Constants
    static constexpr int MAX_SLOT_INDEX = 10;
    static constexpr int SKILL_TYPE_NONE = 1;

    // ========== Game Loop State Machine ==========
    enum class GameSubState {
        IDLE,                       // 非自己回合，或等待中
        AWAITING_MAIN_INPUT,        // 自己回合，显示主菜单
        WAITING_DRAW_RSP,           // 已发送DrawCardReq，等待DrawCardRsp
        AWAITING_DRAWN_DECISION,    // 已抽牌，显示弃牌/替换/技能菜单
        WAITING_DISCARD_RSP,        // 已发送DiscardDrawnReq，等待响应
        AWAITING_REPLACE_SLOTS,     // 等待输入替换槽位
        WAITING_REPLACE_RSP,        // 已发送ReplaceWithDrawnReq，等待响应
        AWAITING_TAKE_SLOTS,        // 等待输入从弃牌堆拿牌的槽位
        WAITING_TAKE_RSP,           // 已发送TakeFromDiscardReq，等待响应
        SKILL_PEEK_SLOT,            // 等待输入偷看自己的槽位
        SKILL_SPY_TARGET,           // 等待输入间谍目标玩家ID
        SKILL_SPY_SLOT,             // 等待输入间谍目标槽位
        SKILL_SWAP_MY_SLOT,         // 等待输入交换自己的槽位
        SKILL_SWAP_TARGET_PLAYER,   // 等待输入交换目标玩家ID
        SKILL_SWAP_TARGET_SLOT,     // 等待输入交换目标槽位
        WAITING_SKILL_RSP,          // 已发送UseSkillReq，等待响应
        WAITING_CALL_STEADY_RSP     // 已发送CallSteadyReq，等待响应
    };

    GameSubState subState_ = GameSubState::IDLE;
    int skillTypePending_ = 0;      // 待处理的技能类型 (2=PEEK_SELF, 3=SPY, 4=SWAP)
    int skillTypeJustCompleted_ = 0; // 刚完成的技能类型（UseSkillRsp后更新手牌用）

    struct SkillPendingData {
        int mySlot = 0;
        int targetSlot = 0;
        int64_t targetPlayerId = 0;
    };
    SkillPendingData skillPending_;

    // ========== 流程方法 ==========
    void connectToServer();
    void loginFlow();
    void roomFlow();
    void waitingRoomLoop();
    void gameLoop();

    // ========== 状态机核心方法 ==========
    void drainMessages(bool render = true);
    bool tryReadLine(std::string& line);
    void handleInputLine(const std::string& line);
    void handleRoundRevealPhase();

    void transitionTo(GameSubState newState);
    bool isExpectingInput() const;
    void showPrompt();

    // ========== 输入处理方法 ==========
    void onMainMenuInput(const std::string& line);
    void onDrawnCardDecision(const std::string& line);
    void onSlotListInput(const std::string& line, bool fromDiscard);
    void onSkillInputLine(const std::string& line);
    void sendSkillRequest();
    void sendSkipSkillRequest();  // 技能牌丢弃但不使用技能时，发空UseSkillReq结束回合

    // ========== 发送辅助 ==========
    bool sendRequestAndWait(game::messages::ClientMessage& req, GameSubState waitState);

    // ========== 解析辅助 ==========
    bool parseInt(const std::string& str, int& out, int min, int max);
    bool parseInt64(const std::string& str, int64_t& out);

    // ========== 错误处理 ==========
    void handleServerError(const game::messages::ServerMessage& msg);

    // ========== 工具方法 ==========
    std::vector<int> parseSlotIndices(const std::string& input);
};

} // namespace cabo
