#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
彻底删除 uGUI 版本的 GameTableUI 及其所有引用
"""

import os
import shutil

BASE_DIR = "/mnt/c/Users/Admin/Desktop/Cabo GameObject/unity dev/Client/Assets/Scripts"

# 要删除的文件列表
files_to_delete = [
    "ClientCore/Game/GameTableUI.cs",
    "ClientCore/Game/GameTableUI.cs.meta",
]

# 要修改的文件（删除 uGUI 引用）
files_to_modify = [
    "ClientCore/Game/GameSceneController.cs",
]

def delete_files():
    """删除 uGUI 相关文件"""
    print("=== 删除 uGUI 文件 ===")
    for rel_path in files_to_delete:
        full_path = os.path.join(BASE_DIR, rel_path)
        if os.path.exists(full_path):
            os.remove(full_path)
            print(f"✅ 已删除: {rel_path}")
        else:
            print(f"⚠️  文件不存在: {rel_path}")

def clean_gamescene_controller():
    """清理 GameSceneController 中的 uGUI 引用"""
    file_path = os.path.join(BASE_DIR, "ClientCore/Game/GameSceneController.cs")

    print(f"\n=== 清理 {file_path} ===")

    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # 新的简化版本（只支持 UI Toolkit）
    new_content = """using Cabo.Client.Network;
using Cabo.Client.Room;
using Cabo.Client.Runtime;
using UnityEngine;

namespace Cabo.Client.Game
{
    /// <summary>
    /// Placed in GameScene. Finds cross-scene ProtoGateway and GameClientController
    /// (on ClientAppBootstrap's DontDestroyOnLoad GameObject) and wires them to GameTableUIToolkit.
    /// </summary>
    public class GameSceneController : MonoBehaviour
    {
        public ProtoGateway Gateway { get; private set; }
        public GameClientController GameCtrl { get; private set; }

        private void Start()
        {
            Debug.Log("[GameSceneController] ===== Start() 开始 =====");

            var bootstrap = FindObjectOfType<ClientAppBootstrap>();
            Debug.Log($"[GameSceneController] FindObjectOfType<ClientAppBootstrap>: {(bootstrap != null ? "找到" : "未找到")}");

            if (bootstrap == null)
            {
                Debug.LogWarning("[GameSceneController] ClientAppBootstrap not found! GameScene 独立运行模式 - 仅测试 UI 显示");

                // 独立运行模式：直接初始化 UI（仅用于测试）
                var uiToolkit = FindObjectOfType<GameTableUIToolkit>();
                if (uiToolkit != null)
                {
                    Debug.Log("[GameSceneController] 独立模式：尝试初始化 GameTableUIToolkit");
                    // 传入 null 参数，UI 会显示但不会连接网络
                    uiToolkit.Initialize(null, null);
                }
                return;
            }

            Debug.Log($"[GameSceneController] ClientAppBootstrap 找到: {bootstrap.name}");

            // ProtoGateway is not a MonoBehaviour - access via RoomClientController
            var roomCtrl = bootstrap.GetComponent<RoomClientController>();
            Debug.Log($"[GameSceneController] RoomClientController: {(roomCtrl != null ? "找到" : "未找到")}");

            if (roomCtrl != null)
            {
                Gateway = roomCtrl.GetGateway<ProtoGateway>();
                Debug.Log($"[GameSceneController] ProtoGateway from RoomController: {(Gateway != null ? "找到" : "未找到")}");
            }

            GameCtrl = bootstrap.GetComponent<GameClientController>();
            Debug.Log($"[GameSceneController] GameClientController: {(GameCtrl != null ? "找到" : "未找到")}");

            if (Gateway == null)
                Debug.LogError("[GameSceneController] ❌ ProtoGateway not found (room controller or gateway missing)!");
            if (GameCtrl == null)
                Debug.LogError("[GameSceneController] ❌ GameClientController missing on bootstrap!");

            // Find and initialize UI Toolkit version (uGUI version has been removed)
            var tableUIToolkit = FindObjectOfType<GameTableUIToolkit>();
            if (tableUIToolkit != null)
            {
                tableUIToolkit.Initialize(Gateway, GameCtrl);
                Debug.Log($"[GameSceneController] GameTableUIToolkit initialized (Gateway: {(Gateway != null ? "✅" : "❌ null")})");
            }
            else
            {
                Debug.LogError("[GameSceneController] ❌ GameTableUIToolkit not found in scene!");
            }
        }
    }
}
"""

    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(new_content)

    print("✅ GameSceneController.cs 已更新（移除所有 uGUI 引用）")

def main():
    print("开始清理 uGUI 版本...")
    print("=" * 60)

    # 1. 删除文件
    delete_files()

    # 2. 清理引用
    clean_gamescene_controller()

    print("\n" + "=" * 60)
    print("✅ 清理完成！")
    print("\n下一步：")
    print("1. 在 Unity Editor 中，如果场景里有 uGUI 的 GameObject，手动删除它")
    print("2. 打开 GameScene，确保只有 UI Toolkit 版本的 UI")
    print("3. 测试游戏")

if __name__ == "__main__":
    main()
