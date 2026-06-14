# 糖糖 Cabo 美术资源替换实施规划

> 审查范围：C++ 服务端、Protobuf、Unity 客户端 `unity dev/New Client_Unity_Base_Cli`、当前运行画面、游戏策划文档与展示 PPT。
>
> 结论日期：2026-06-14。

## 1. 审查结论

当前游戏逻辑已经具备稳定的美术换皮基础，推荐保持服务端规则和网络状态不变，在 Unity 客户端增加一层“数值/状态 -> 美术定义”的映射。

可以不改服务端直接完成：

- 0-13 数字牌替换为 14 种食物牌。
- 卡牌正面、牌背、技能图标、牌库和弃牌堆视觉。
- 主页、等待房间、牌桌、聊天、结算和排名的背景、面板、按钮、字体与图标。
- 玩家头像、聊天贴纸的资源替换。
- 抽牌、翻牌、交换、技能、CABO 和结算动画的美术升级。
- 音效和背景音乐接入。

需要少量代码适配但不改游戏规则：

- 新增统一的美术资源目录和 `CaboArtCatalog` 数据资产。
- 让 `CardView`、结算页和 UI Toolkit 面板统一从美术目录取图，不再直接画纯色矩形。
- 将 `UITheme` 中硬编码的颜色和运行时生成图标转为可配置皮肤。
- 扩展现有构建前资源检查，保证 Windows 构建和编辑器显示一致。

唯一可能需要改 Protobuf/服务端的功能：

- **玩家自主选择的角色要在所有客户端保持一致。** 当前选择只保存在本机 `PlayerPrefs`；其他客户端会按 `playerId` 自动分配头像，并不知道该玩家真正选择了哪个角色。
- 若接受“角色由座位或玩家 ID 自动分配”，则服务端完全不用改。
- 若要求真实同步选择，应给创建/加入房间请求和 `PlayerPublicInfo` 增加稳定的 `character_id`，服务端仅保存并广播该字符串或整数，不参与任何数值结算。

## 2. 当前代码基础

### 2.1 服务端边界清晰

- `Proto/common.proto` 的 `CardInfo` 只包含 `card_id`、0-13 的 `value`、`skill` 和公开状态。
- `GameService.cc` 根据牌值生成牌并推导 7-12 的技能，不含图片、颜色、食物名称或资源路径。
- 回合结算已经下发每个玩家最终的 `card_values`、罚分、单轮分数和累计分数，足够驱动“角色逐个吃食物”的客户端动画。
- 贴纸协议只广播稳定的 `sticker_pack + sticker_name`，服务端不读取图片文件。

因此食物名称、插图、颜色、技能图标和动画都应留在客户端，不应把资源路径加入游戏协议。

### 2.2 Unity UI 是混合结构

- `GameBootstrap.cs` 在运行时自动创建 `UIDocument` 和 UI 管理器。
- `GameScreen.uxml` 只是空壳，主页、房间和大部分牌桌 UI 都由 C# 动态创建。
- `UITheme.cs` 保存大量硬编码颜色，并在代码中生成皇冠图标。
- 对局卡牌已经迁移到持久化的 uGUI `CardView`/`CardTableView`，动画通过同一个根 `RectTransform` 移动卡牌，这是最适合接入真实牌面的替换点。
- 回合结算仍用 `GameTablePanel.CreateRevealCard()` 创建 UI Toolkit 数字牌，尚未复用 `CardView` 的美术。
- `CaboGameScene` 当前只有摄像机和灯光；现有画面基本全部由运行时 UI 覆盖。仅向场景中放背景模型或 Sprite 不会自动形成最终牌桌，需要明确放在 UI 背景层或调整 Canvas/UI Toolkit 结构。

### 2.3 现有资源链路只覆盖头像和贴纸

- `Assets/Art/Avatars` 和 `Assets/Art/Stickers` 是编辑器源目录。
- `ArtResourceBuildProcessor.cs` 在构建前复制到 `Assets/Resources/Art` 并生成 `manifest.json`。
- `PlayerProfileStore.cs` 在编辑器读取文件，在构建中读取 `Resources` 清单。
- 当前没有食物牌、UI 背景、图标、Prefab、AnimationClip 或 AudioClip 资源。
- 当前 8 张测试 PNG 均为 1254x1254、Default Texture、开启 mipmap，不适合作为最终 UI 导入设置。

## 3. 推荐资源架构

本项目是固定内容的课程展示版本，暂不建议引入 Addressables。继续使用 Unity 原生资源引用和 `Resources` 入口，改动最小且构建风险最低。

建议新增：

```text
Assets/Art/
  Cards/
    Foods/food_00_water.png ... food_13_bubble_tea.png
    Frames/card_frame_low.png ...
    Backs/card_back_default.png
  Characters/
    strawberry/
    oat/
    pomelo/
    bean/
  UI/
    Backgrounds/
    Panels/
    Buttons/
    Icons/
  Stickers/
  Audio/
    BGM/
    SFX/

Assets/Resources/Art/
  CaboArtCatalog.asset
```

`CaboArtCatalog` 是唯一运行时入口，引用 `Assets/Art` 中的 Sprite、AudioClip 和字体。因为 Catalog 本身位于 `Resources`，它引用的外部资源也会被 Unity 自动打入构建，不需要再复制食品牌和 UI 图片。

建议 Catalog 包含：

```text
FoodCardDefinition[14]
  value
  displayName
  category
  sugarEnergyLabel
  skillType
  foodSprite
  frameSprite
  skillIcon

CardSkin
  backSprite
  selectedFrame
  unknownCardSprite
  drawPileSprite

CharacterDefinition[4]
  characterId
  displayName
  avatarSprite
  portraitSprite
  happy/eating/fail 表情或序列帧

UiSkin
  homeBackground
  tableBackground
  settlementBackground
  panelSprite
  buttonSprites
  crown/cabo/penalty 等图标

AudioSkin
  draw/flip/discard/swap/skill/cabo/eat/penalty/victory
```

在代码侧增加一个很薄的 `CaboArt`/`CardArtProvider` 访问层：启动时加载 Catalog，按牌值或角色 ID 查询定义；资源缺失时回退到当前纯色数字牌，避免因为单张图片缺失导致游戏不可玩。

## 4. 可替换矩阵

| 模块 | 当前实现 | 替换方式 | 服务端影响 | 风险 |
|---|---|---|---|---|
| 食物牌正面 | `CardView` 纯色 + 数字 + 文字技能角标 | Catalog 按 0-13 设置食物图、边框、名称、数值和技能图标 | 无 | 低 |
| 牌背/牌库 | 蓝色矩形和 `CABO` 文本 | 统一牌背 Sprite，牌库可用 2-3 层偏移 | 无 | 低 |
| 对局动画 | 持久化 `CardView` 移动/翻转 | 保留根对象和动画，仅替换子 Image/Text | 无 | 低 |
| 结算牌 | UI Toolkit 临时数字牌 | 读取同一 FoodCardDefinition，或改用结算专用 CardView | 无 | 中 |
| 结算吃食物 | 当前为静态列表和分数 | 逐玩家、逐牌飞向角色，糖能数字累加，最后显示罚分 | 无 | 中高 |
| 主页/房间/牌桌 | C# 动态创建 + `UITheme` 纯色 | 背景图、九宫格面板、按钮 Sprite、图标和 USS/C# 皮肤参数 | 无 | 中 |
| 角色头像 | 本地文件/Resources manifest | Catalog 角色定义或保留现有头像清单 | 可选 | 低 |
| 角色选择同步 | 本机 `PlayerPrefs` | 增加 `character_id` 并随房间公开信息广播 | 需要小改协议 | 中 |
| 聊天贴纸 | `pack/name` + manifest | 保持逻辑 ID 不变，只替换同名图片 | 无 | 低 |
| 场景背景 | 场景只有 Camera/Light | 优先放 UI 最底层背景；需要景深时再做场景 Sprite/3D | 无 | 中 |
| 音效/BGM | 尚无资源 | 新增 AudioManager 或轻量 AudioSource 池 | 无 | 中 |

## 5. 分阶段实施

### 阶段 A：建立资源契约和回退机制

1. 创建 `CaboArtCatalog`、`FoodCardDefinition`、`CharacterDefinition`。
2. 固定 14 张食物牌的 value 映射，禁止用数组下标以外的临时命名推断规则。
3. 增加编辑器校验：牌值 0-13 必须唯一且完整，关键 Sprite 不得为空。
4. 保留当前纯色牌作为 fallback。
5. 增加 UI 图片导入 Preset：Sprite (2D and UI)、mipmap 关闭、透明通道开启、Clamp、合理压缩。

验收：删除任意一张测试图后仍能进入游戏；Catalog 校验能明确指出缺失 value。

### 阶段 B：先替换卡牌，不动动画逻辑

1. 将 `CardView` 拆成固定根和可换皮子节点：背景/边框、食物图、数值、名称、技能图标。
2. `ShowFront(value)` 只负责从 Catalog 取定义并刷新表现。
3. `ShowBack()` 使用统一牌背。
4. 不修改 `CardTableView` 的位置、选择、移动、翻转和队列逻辑。
5. 让牌库、弃牌堆、抽到的牌、技能预览和临时动画牌全部走同一 `CardView`。
6. 将结算页的 `CreateRevealCard()` 接到同一 FoodCardDefinition，消除对局牌与结算牌两套样式。

验收：0-13 全部可显示；暗牌不泄露食物图；7-12 技能图标正确；替换前后的所有动画时序不变。

### 阶段 C：替换 UI 皮肤和牌桌背景

1. 在 UI 根节点增加 `BackgroundLayer`，主页、房间、牌桌、结算按状态切换背景图。
2. 将 `UITheme` 的颜色和图标读取改为 Catalog/UiSkin，可保留当前常量作为回退。
3. 面板和按钮优先使用九宫格 Sprite，避免不同分辨率拉伸变形。
4. 将最常见的动态 inline style 收口为少量主题方法；不必一次性重写全部 `GameTablePanel`。
5. 保持 UI Toolkit 负责房间、聊天、按钮和结算，uGUI 负责牌桌卡牌，避免重新迁移 UI 框架。

验收：1366x768 与 1920x1080 下不裁切；背景不会遮住 uGUI 卡牌 Canvas；聊天和按钮文字对比度足够。

### 阶段 D：角色资源与同步策略

先选定以下一种方案：

- MVP 方案：四个角色按座位或 `playerId` 稳定分配，不改服务端；首页只做预览，不承诺多人同步选择。
- 完整方案：新增 `character_id`，创建/加入房间时上传，服务端写入 `PlayerPublicInfo`，重连和房间广播同步。

完整方案的最小字段变更：

```text
CreateRoomReq.character_id
JoinRoomReq.character_id
PlayerPublicInfo.character_id
```

服务端只校验长度和允许字符，并原样保存/广播；客户端遇到未知 ID 时使用默认角色。不要传图片路径，也不要让角色 ID 参与规则或胜负计算。

### 阶段 E：结算小剧场

状态：**已完成（2026-06-14）**。普通结算、错误健康宣言和糖分反转套餐均已通过事件顺序验收；最终显示直接采用服务端 `RoundScore/CumulativeScore`，演出结束前禁用下一轮控件。

1. 使用现有 `RoundRevealNotify` 的 `card_values` 和分数，不新增网络字段。
2. 为每个玩家生成结算队列：亮出最终食物 -> 食物飞向角色/餐盘 -> 数字累加。
3. 错误健康宣言在普通食物后播放“翻车小点心 +10”。
4. “糖分反转套餐”播放单独徽章和其他玩家 +50 的提示。
5. 动画全部结束后再开放下一轮准备按钮。
6. 低成本 MVP 可采用静态角色图 + 2-3 张表情序列；不必第一版就引入骨骼动画。

验收：动画累计结果必须与服务端 `round_score/cumulative_score` 完全一致；动画只解释结果，不自行计算结果。

### 阶段 F：音效、贴纸和构建检查

状态：**已完成（2026-06-14）**。已生成并接入 9 个原创程序化音效，头像/贴纸清单与导入规格已加入构建前校验；Windows 64-bit 正式构建为 0 error、0 warning，并通过可执行文件启动冒烟检查。

1. 接入抽牌、翻牌、弃牌、技能、CABO、吃食物、罚分和胜利音效。
2. 贴纸保持 `pack/name` 稳定，只换同名图片；如改名，所有客户端版本必须同时更新。
3. 调整现有头像/贴纸导入设置，1254x1254 测试图建议降到 512 或 1024，并关闭 UI mipmap。
4. 扩展构建前检查：Catalog 完整、资源 ID 唯一、贴纸 manifest 可读取、关键背景存在。
5. 在 Windows 实际构建中验证，不能只在 Unity Editor 里验收。

## 6. 美术交付规范

### 食物牌

- 推荐画布：512x768 或 768x1024，统一纵横比。
- 食物主体最好单独透明 PNG，卡框和背景分层，便于低糖/高糖换色和统一调整。
- 牌面必须保留：食物图、名称、0-13 糖能值、7-12 技能图标。
- 小尺寸对手牌中仍需优先看清糖能值和颜色分级，食物名称可按空间隐藏。

### UI 和背景

- 主背景按 16:9 交付，建议 1920x1080；关键内容放在安全区内。
- 面板、按钮和标签框使用可九宫格切片的透明 PNG。
- 牌桌背景中心必须给牌库、弃牌堆和操作按钮保留低细节区域。

### 角色

- 头像：512x512 透明 PNG。
- 半身/结算立绘：建议 1024 高度，透明背景。
- MVP 表情：默认、开心/进食、失败三态即可。
- `character_id` 使用稳定 ASCII，例如 `strawberry`、`oat`、`pomelo`、`bean`，显示名称可为中文。

### 贴纸

- 推荐 512x512 透明 PNG。
- 资源 ID 使用稳定 ASCII 文件名；显示文案与文件名分离。
- 避免在版本间复用同一个 ID 表达完全不同含义。

## 7. 回归测试重点

- 4 人开局、抽牌、弃牌、单张/多张替换、失败加牌、偷看自己、侦查、交换和 CABO 全流程。
- 暗牌始终显示牌背，私密技能值只对行动玩家可见。
- 0、7、9、11、13 等边界牌值的图、数值和技能标识正确。
- 回合结算、错误宣言 +10、反转套餐、恰好 100 重置和最终排名显示正确。
- 角色在主页、房间、聊天、牌桌、结算和排名中一致。
- 贴纸资源缺失时有占位或文本回退，不抛异常。
- 编辑器和 Windows 构建的资源结果一致。
- Unity Console 0 error；关键分辨率截图对比；动画队列结束后才能进入结算或下一轮。

## 8. 预计工作量

不含美术绘制时间，开发量可按以下范围估算：

| 工作 | 预计开发时间 |
|---|---:|
| Catalog、Provider、导入规范和校验 | 1-2 天 |
| 牌面/牌背接入并回归现有动画 | 2-3 天 |
| UI 背景、面板、按钮和主题参数化 | 2-4 天 |
| 角色显示 | 1-2 天 |
| 角色跨客户端同步（可选） | 1-2 天 |
| 结算小剧场 | 3-5 天 |
| 音效、构建检查和完整回归 | 1-2 天 |

建议顺序是：**资源契约 -> 食物牌 -> 全局 UI -> 角色 -> 结算动画 -> 音效与构建验收**。食物牌完成后就能形成第一版可展示的主题化成果，后续步骤可以逐项叠加而不阻塞游戏逻辑。
