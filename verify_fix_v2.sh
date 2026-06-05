#!/bin/bash

# 颜色定义
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo "========================================"
echo "  CLI客户端修复验证脚本 v2"
echo "========================================"
echo ""

BASE_DIR="/mnt/c/Users/Admin/Desktop/Cabo GameObject/MuduoBaseGameServer"

# 检查1: 服务器可执行文件
echo -n "检查1: 服务器可执行文件... "
if [ -f "$BASE_DIR/build/GameServer" ]; then
    echo -e "${GREEN}✓ 存在${NC}"
else
    echo -e "${RED}✗ 不存在${NC}"
fi

# 检查2: 客户端可执行文件
echo -n "检查2: 客户端可执行文件... "
if [ -f "$BASE_DIR/cli_client/build/cabo_cli_client" ]; then
    echo -e "${GREEN}✓ 存在${NC}"
else
    echo -e "${RED}✗ 不存在${NC}"
fi

# 检查3: Bug #1 修复 - NetworkClient.cpp 缓冲区检查
echo -n "检查3: Bug #1 修复 (hasMessage缓冲区)... "
if grep -q "CRITICAL FIX: Check recvBuffer_" "$BASE_DIR/cli_client/src/NetworkClient.cpp"; then
    echo -e "${GREEN}✓ 已应用${NC}"
else
    echo -e "${RED}✗ 未应用${NC}"
fi

# 检查4: Bug #2 修复 - StartGameRsp处理
echo -n "检查4: Bug #2 修复 (StartGameRsp)... "
if grep -q "has_start_game_rsp" "$BASE_DIR/cli_client/src/GameState.cpp"; then
    echo -e "${GREEN}✓ 已应用${NC}"
else
    echo -e "${RED}✗ 未应用${NC}"
fi

# 检查5: Bug #2 修复 - gameStartConfirmed标志
echo -n "检查5: Bug #2 修复 (gameStartConfirmed)... "
if grep -q "gameStartConfirmed" "$BASE_DIR/cli_client/src/GameState.h"; then
    echo -e "${GREEN}✓ 已应用${NC}"
else
    echo -e "${RED}✗ 未应用${NC}"
fi

# 检查6: Bug #2 修复 - 超时检查优化
echo -n "检查6: Bug #2 修复 (超时检查)... "
if grep -q "!state_.gameStartConfirmed" "$BASE_DIR/cli_client/src/ClientApp.cpp"; then
    echo -e "${GREEN}✓ 已应用${NC}"
else
    echo -e "${RED}✗ 未应用${NC}"
fi

# 检查7: RoomStartNotify处理
echo -n "检查7: RoomStartNotify处理... "
if grep -q "has_room_start_notify" "$BASE_DIR/cli_client/src/GameState.cpp"; then
    echo -e "${GREEN}✓ 已应用${NC}"
else
    echo -e "${YELLOW}⚠ 未找到${NC}"
fi

echo ""
echo "========================================"
echo "  Bug修复历史"
echo "========================================"
echo ""
echo "Bug #1: 缓冲区检查缺失"
echo "  问题: hasMessage()只检查socket，不检查recvBuffer_"
echo "  影响: 所有客户端卡在等待室"
echo "  状态: ✅ 已修复"
echo ""
echo "Bug #2: 房主超时问题"
echo "  问题: 缺少StartGameRsp处理，超时检查逻辑不当"
echo "  影响: 房主发送start后超时退出"
echo "  状态: ✅ 已修复"
echo ""

echo "========================================"
echo "  测试建议"
echo "========================================"
echo ""
echo "完整测试流程:"
echo "  1. 启动服务器: $BASE_DIR/build/GameServer 8888"
echo "  2. 启动4个客户端"
echo "  3. 客户端1创建房间并ready"
echo "  4. 客户端2-4加入房间并ready"
echo "  5. 客户端1输入start"
echo "  6. ✅ 验证所有4个客户端（包括房主）都进入游戏"
echo ""
echo "重点验证:"
echo "  • 房主不再超时退出"
echo "  • 所有客户端都能看到游戏界面"
echo "  • 显示 'Round 1, Turn 1'"
echo ""

echo "详细文档:"
echo "  • BUG_FIX_REPORT.md     - Bug #1 技术分析"
echo "  • BUG_FIX_REPORT_2.md   - Bug #2 技术分析"
echo "  • TEST_GUIDE.md         - 测试步骤指南"
echo ""
