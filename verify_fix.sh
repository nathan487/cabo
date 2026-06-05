#!/bin/bash

# 颜色定义
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo "========================================"
echo "  CLI客户端修复验证脚本"
echo "========================================"
echo ""

BASE_DIR="/mnt/c/Users/Admin/Desktop/Cabo GameObject/MuduoBaseGameServer"

# 检查1: 服务器可执行文件
echo -n "检查1: 服务器可执行文件... "
if [ -f "$BASE_DIR/build/GameServer" ]; then
    echo -e "${GREEN}✓ 存在${NC}"
else
    echo -e "${RED}✗ 不存在${NC}"
    echo "  请先编译服务器: cd $BASE_DIR/build && cmake .. && make"
fi

# 检查2: 客户端可执行文件
echo -n "检查2: 客户端可执行文件... "
if [ -f "$BASE_DIR/cli_client/build/cabo_cli_client" ]; then
    echo -e "${GREEN}✓ 存在${NC}"
else
    echo -e "${RED}✗ 不存在${NC}"
    echo "  请先编译客户端: cd $BASE_DIR/cli_client/build && cmake .. && make"
fi

# 检查3: NetworkClient.cpp 修复是否应用
echo -n "检查3: NetworkClient.cpp 修复... "
if grep -q "CRITICAL FIX: Check recvBuffer_" "$BASE_DIR/cli_client/src/NetworkClient.cpp"; then
    echo -e "${GREEN}✓ 已应用${NC}"
else
    echo -e "${RED}✗ 未应用${NC}"
    echo "  修复代码未找到，请检查 NetworkClient.cpp"
    exit 1
fi

# 检查4: hasMessage() 是否检查缓冲区
echo -n "检查4: hasMessage() 缓冲区检查... "
if grep -A 5 "bool NetworkClient::hasMessage" "$BASE_DIR/cli_client/src/NetworkClient.cpp" | grep -q "recvBuffer_.size()"; then
    echo -e "${GREEN}✓ 已实现${NC}"
else
    echo -e "${RED}✗ 未实现${NC}"
    echo "  hasMessage() 没有检查 recvBuffer_"
    exit 1
fi

# 检查5: 客户端编译时间
echo -n "检查5: 客户端编译时间... "
CLIENT_MTIME=$(stat -c %Y "$BASE_DIR/cli_client/build/cabo_cli_client" 2>/dev/null || echo "0")
SOURCE_MTIME=$(stat -c %Y "$BASE_DIR/cli_client/src/NetworkClient.cpp" 2>/dev/null || echo "0")

if [ "$CLIENT_MTIME" -gt "$SOURCE_MTIME" ]; then
    echo -e "${GREEN}✓ 最新${NC}"
else
    echo -e "${YELLOW}⚠ 需要重新编译${NC}"
    echo "  源文件比可执行文件新，请重新编译客户端"
fi

# 检查6: GameState.cpp 的 GameStartNotify 处理
echo -n "检查6: GameStartNotify 处理... "
if grep -q "has_game_start_notify" "$BASE_DIR/cli_client/src/GameState.cpp" && \
   grep -q "phase = PLAYING" "$BASE_DIR/cli_client/src/GameState.cpp"; then
    echo -e "${GREEN}✓ 已实现${NC}"
else
    echo -e "${RED}✗ 未实现${NC}"
    echo "  GameState 没有正确处理 GameStartNotify"
fi

# 检查7: waitingRoomLoop 的 phase 检查
echo -n "检查7: waitingRoomLoop phase 检查... "
if grep -A 5 "void ClientApp::waitingRoomLoop" "$BASE_DIR/cli_client/src/ClientApp.cpp" | grep -q "phase == GameState::PLAYING"; then
    echo -e "${GREEN}✓ 已实现${NC}"
else
    echo -e "${YELLOW}⚠ 需要确认${NC}"
    echo "  请手动检查 waitingRoomLoop 是否检查 phase"
fi

echo ""
echo "========================================"
echo "  验证总结"
echo "========================================"
echo ""

# 给出建议
if grep -q "CRITICAL FIX" "$BASE_DIR/cli_client/src/NetworkClient.cpp" && \
   [ -f "$BASE_DIR/cli_client/build/cabo_cli_client" ]; then
    echo -e "${GREEN}✓ 修复已正确应用${NC}"
    echo ""
    echo "下一步操作:"
    echo "  1. 启动服务器: cd $BASE_DIR/build && ./GameServer 8888"
    echo "  2. 启动4个客户端终端"
    echo "  3. 按照 TEST_GUIDE.md 的步骤进行测试"
    echo ""
    echo "快速测试命令:"
    echo "  终端1: $BASE_DIR/build/GameServer 8888"
    echo "  终端2-5: $BASE_DIR/cli_client/build/cabo_cli_client"
else
    echo -e "${RED}✗ 修复未完全应用${NC}"
    echo ""
    echo "请执行:"
    echo "  cd $BASE_DIR/cli_client/build"
    echo "  cmake .."
    echo "  make"
fi

echo ""
echo "详细测试指南: TEST_GUIDE.md"
echo "技术分析报告: BUG_FIX_REPORT.md"
echo ""
