# Beijing Claude Translator

一个很小的 Windows 悬浮工具，主要给 Claude 用户用。

## 功能

- 悬浮显示北京时间。
- 中文转英文，方便把中文想法发给 Claude。
- 英文转中文，方便看英文回复或资料。
- 关闭窗口后可留在系统托盘。
- 支持开机自启。
- 支持日间 / 夜间模式。

## 适合谁

如果你把电脑时区改成海外，但仍然想看北京时间，并且想用中文输入、英文和 Claude 聊天，这个工具就是为这个场景做的。

## Windows 使用方式

下载 CI 生成的 `BeijingClaudeTranslator-win.zip`，解压后双击：

```text
Start-ClockTranslator.vbs
```

这个启动方式没有黑框终端。

如果需要桌面图标，双击：

```text
Install-DesktopShortcut.cmd
```

## 说明

默认使用公共在线翻译接口，适合日常轻量使用。公共接口慢或失败时，可以稍后重试。

长期稳定使用可以自己配置 Microsoft Translator 或 DeepL。

## License

MIT
