#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""批量替换 Unity C# 脚本中的中文文本为英文"""

import os
import re

# 中文 -> 英文映射表
replacements = {
    # 玩家相关
    "你": "You",
    "对手": "Opponent",
    "玩家": "Player",

    # 游戏状态
    "第": "Round",
    "轮": "",
    "回合": "Turn",
    "分": "pts",
    "等待回合开始": "Waiting for turn",
    "等待": "Waiting",
    "轮到你行动": "YOUR TURN",
    "轮到你了": "YOUR TURN",
    "★ 轮到你行动 ★": "YOUR TURN",
    "★ 轮到你了! ": "YOUR TURN! ",

    # 动作
    "抽牌": "Draw",
    "抽到": "Drew",
    "弃牌": "Discard",
    "弃掉": "Discard",
    "替换": "Replace",
    "稳态": "Cabo",
    "稳态!": "Cabo!",
    "技能牌": "Skill card",
    "技能": "Skill",
    "拿弃牌": "Take Discard",
    "从弃牌堆拿走了牌": "took from discard",
    "从牌库抽了1张牌": "drew a card",
    "弃掉了牌": "discarded",
    "替换了卡牌": "replaced card",

    # 房间相关
    "未加入房间": "Not in room",
    "请先 Connect，然后 Create Room": "Connect, then Create Room",
    "已离开房间": "Left room",
    "请重新 Connect / Create Room": "Reconnect / Create Room",
    "已断开连接": "Disconnected",
    "状态: 未连接": "Status: Disconnected",
    "状态:": "Status:",
    "未连接": "Disconnected",
    "连接中": "Connecting",
    "已连接": "Connected",
    "重连中": "Reconnecting",
    "昵称:": "Name:",
    "房间码:": "Code:",
    "复制房码": "Copy Code",
    "离开/断开": "Disconnect",
    "房间码已复制": "Room code copied",
    "你的ID": "Your ID",

    # 结算
    "游戏结束": "GAME OVER",
    "轮结算": "Results",
    "神风": "KAMIKAZE",
    "罚": "Penalty",
    "手牌": "Hand",
    "本轮": "Round",
    "累计": "Total",

    # UI
    "UI 就绪. 请先点击 Connect": "UI Ready. Click Connect first",
    "测试模式 - UI 预览": "Test Mode - UI Preview",
    "字体显示测试：中文测试": "Font test: English",
    "UI Toolkit 测试模式": "UI Toolkit Test Mode",
    "网络测试": "Network Test",

    # 数字前缀
    "第 ": "Round ",
    " 轮": "",

    # 其他
    "成功": "Success",
    "失败": "Failed",
    "当前": "Current",
    "等待中...": "Waiting...",
    "行动": "action",
}

def replace_in_file(filepath):
    """替换单个文件中的中文"""
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()

        original_content = content

        # 按照映射表替换
        for cn, en in replacements.items():
            content = content.replace(cn, en)

        # 特殊处理：清理多余的空格
        content = re.sub(r'Round\s+(\d+)\s+', r'Round \1 ', content)

        # 只有内容改变时才写入
        if content != original_content:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(content)
            return True
        return False
    except Exception as e:
        print(f"Error processing {filepath}: {e}")
        return False

def main():
    """主函数：遍历所有 C# 文件"""
    base_dir = "/mnt/c/Users/Admin/Desktop/Cabo GameObject/unity dev/Client/Assets/Scripts"

    modified_count = 0
    total_count = 0

    for root, dirs, files in os.walk(base_dir):
        for file in files:
            if file.endswith('.cs'):
                filepath = os.path.join(root, file)
                total_count += 1
                if replace_in_file(filepath):
                    modified_count += 1
                    print(f"Modified: {filepath}")

    print(f"\n✅ Complete! Modified {modified_count} out of {total_count} files.")

if __name__ == "__main__":
    main()
