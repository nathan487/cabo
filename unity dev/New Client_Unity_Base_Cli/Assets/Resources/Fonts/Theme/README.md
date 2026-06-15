# UI 主题字体

- `NotoSansSC-Variable.ttf`：正文、数字与中英文混排。来源于 Google Fonts 的 Noto Sans SC，采用 SIL Open Font License 1.1，授权文本见 `OFL-NotoSansSC.txt`。
- `ZCOOLKuaiLe-Regular.ttf`：纯中文标题与主要中文按钮。来源于 Google Fonts 的 ZCOOL KuaiLe，采用 SIL Open Font License 1.1，授权文本见 `OFL-ZCOOLKuaiLe.txt`。

运行时由 `UITheme` 从 `Resources/Fonts/Theme` 加载。含拉丁字母的标题和按钮自动使用 Noto Sans SC，避免主题字体缺少英文字形时出现方框。
