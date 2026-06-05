#!/bin/bash

# 颜色定义
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m'

clear

echo -e "${BLUE}╔════════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║                                                                ║${NC}"
echo -e "${BLUE}║          CLI客户端Bug修复 - 快速启动指南                      ║${NC}"
echo -e "${BLUE}║                                                                ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════════════════════╝${NC}"
echo ""

BASE_DIR="/mnt/c/Users/Admin/Desktop/Cabo GameObject/MuduoBaseGameServer"

echo -e "${YELLOW}📋 可用文档:${NC}"
echo "  1. SUMMARY.md          - 完成总结和快速导航"
echo "  2. BUG_FIX_REPORT.md   - 技术分析报告（详细）"
echo "  3. TEST_GUIDE.md       - 测试步骤指南（推荐先看）"
echo "  4. DEBUGGING_GUIDE.md  - 调试和故障排除"
echo ""

echo -e "${YELLOW}🔍 修复状态:${NC}"
if grep -q "CRITICAL FIX: Check recvBuffer_" "$BASE_DIR/cli_client/src/NetworkClient.cpp"; then
    echo -e "  ${GREEN}✓ 修复已应用到源代码${NC}"
else
    echo -e "  ${RED}✗ 修复未找到${NC}"
    exit 1
fi

if [ -f "$BASE_DIR/cli_client/build/cabo_cli_client" ]; then
    echo -e "  ${GREEN}✓ 客户端已编译${NC}"
else
    echo -e "  ${RED}✗ 客户端未编译${NC}"
    exit 1
fi

echo ""
echo -e "${YELLOW}🚀 快速测试命令:${NC}"
echo ""
echo "  打开5个终端，分别运行:"
echo ""
echo -e "  ${GREEN}终端1 (服务器):${NC}"
echo "  cd \"$BASE_DIR/build\" && ./GameServer 8888"
echo ""
echo -e "  ${GREEN}终端2-5 (客户端1-4):${NC}"
echo "  cd \"$BASE_DIR/cli_client/build\" && ./cabo_cli_client"
echo ""

echo -e "${YELLOW}📖 测试步骤简要:${NC}"
echo ""
echo "  1. 启动服务器（终端1）"
echo "  2. 启动4个客户端（终端2-5）"
echo "  3. 客户端1: 输入 127.0.0.1:8888 → 选1创建房间 → 输入ready"
echo "  4. 客户端2-4: 输入 127.0.0.1:8888 → 选2加入房间 → 输入房间码 → 输入ready"
echo "  5. 客户端1 (房主): 输入 start"
echo "  6. ✓ 所有客户端应该进入游戏界面"
echo ""

echo -e "${YELLOW}📌 期望看到:${NC}"
echo ""
echo "  >>> Game starting! Transitioning to game loop..."
echo "  [DEBUG] Current phase: PLAYING, breaking from waitingRoomLoop"
echo ""
echo "  ================================================================================"
echo "                          Cabo Game - 4 Players"
echo "                            Round 1, Turn 1"
echo "  ================================================================================"
echo ""

echo -e "${YELLOW}❌ 如果失败:${NC}"
echo ""
echo "  1. 运行: bash verify_fix.sh"
echo "  2. 查看: DEBUGGING_GUIDE.md"
echo "  3. 检查服务器日志是否有错误"
echo ""

echo -e "${YELLOW}💡 提示:${NC}"
echo ""
echo "  • 详细测试步骤: 查看 TEST_GUIDE.md"
echo "  • 技术细节: 查看 BUG_FIX_REPORT.md"
echo "  • 调试方法: 查看 DEBUGGING_GUIDE.md"
echo ""

echo -e "${GREEN}准备就绪！按任意键退出...${NC}"
read -n 1 -s

echo ""
