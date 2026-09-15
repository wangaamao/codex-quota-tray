# Codex Quota Tray（Codex 余量条）

[English README](README.md)

**搜索关键词：** Codex、Codex 余量、Codex 剩余用量、ChatGPT Codex、ChatGPT 使用限额、5 小时余量、一周余量、额度重置时间、限额监控、用量监控、Windows 任务栏插件、OpenAI Codex 用量查询、Codex quota、remaining usage、usage limits、rate limit monitor。

Codex Quota Tray 是一个小巧、开源的 Windows 悬浮工具，用来显示当前 Codex 账户的 5 小时和一周剩余用量，以及对应的重置时间。

它可以始终显示在 Windows 任务栏及普通窗口上方，根据文字自动调整大小，默认每 5 分钟刷新一次，并且只允许运行一个实例。

> [!IMPORTANT]
> Codex Quota Tray **本身不会直接发起任何网络请求**。它不包含网络通信代码，不连接第三方服务器，不读取浏览器数据、对话内容、项目源码或原始登录令牌。它只是向本机已经安装的 Codex 进程查询当前用户的额度数据。为了返回实时数据，Codex 自身可能像正常使用时一样连接 OpenAI。

## 功能

- 显示 Codex 5 小时窗口的剩余百分比。
- 显示 Codex 一周窗口的剩余百分比。
- 使用电脑本地时区显示两个重置时间。
- 默认每 5 分钟自动刷新，右键菜单可选择 1、3、5 或 10 分钟。
- 刷新失败后自动连续重试最多 3 次，然后恢复选定的正常刷新周期。
- 持续保持在 Windows 任务栏和普通窗口上方。
- 根据显示文字自动调整尺寸，仅保留少量边距。
- 可以拖动到任意位置。
- 双击可以打开或切换到 Codex。
- 右键菜单提供立即刷新、刷新间隔设置、打开 Codex 和退出功能。
- 使用 Windows 全局互斥锁，防止重复运行。
- 发布为体积很小的独立 Windows EXE。

## 快速开始

1. 安装 Codex CLI，或者安装 VS Code 的 OpenAI Codex 扩展。
2. 使用当前 Windows 账户登录 Codex。
3. 下载 Release，双击 `CodexQuotaTray.exe`。

运行编译好的 EXE 不需要安装 Python、Node.js、Visual Studio，不需要填写 API Key，也不需要手动创建配置文件。

## 建议的 GitHub 仓库信息

仓库简介：

```text
一个显示 Codex 5 小时和一周余量、剩余用量及重置时间的轻量 Windows 任务栏工具。
```

建议添加的 GitHub Topics：

```text
codex, chatgpt, openai, codex-quota, remaining-usage, usage-limits,
rate-limit-monitor, quota-monitor, windows, windows-taskbar, winforms
```

可以在 CMD 或 PowerShell 中检查 Codex：

```powershell
codex --version
codex login status
```

如果尚未登录：

```powershell
codex login
```

## 工作原理

程序只执行以下本地流程：

```text
CodexQuotaTray.exe
        |
        | 通过标准输入输出启动本地进程
        v
codex login status
        |
        | 已登录
        v
codex app-server --stdio
        |
        | 本地 JSON-RPC：account/rateLimits/read
        v
5 小时和一周的剩余百分比及重置时间
```

每次刷新时，程序会：

1. 在本机查找 `codex.exe`。
2. 执行 `codex login status`，确认当前 Windows 用户已经登录。
3. 启动本地子进程 `codex app-server --stdio`。
4. 通过重定向的标准输入发送 `account/rateLimits/read` 请求。
5. 从重定向的标准输出读取响应。
6. 只在内存中保留两个剩余百分比和两个重置时间。
7. 关闭标准输入，让本次临时启动的 app-server 子进程正常退出。

如果首次尝试失败，程序会以 1.5 秒间隔最多再尝试 3 次。无论重试成功还是全部失败，下一次定时刷新都会恢复为用户选定的正常周期。刷新间隔保存在 `%LOCALAPPDATA%\CodexQuotaTray\settings.txt`，其中只有选定的分钟数。

本仓库没有独立后端、遥测服务、分析 SDK、自动更新器、数据库或隐藏的常驻后台服务。

## 隐私与联网说明

### 本应用不会做什么

Codex Quota Tray 不会：

- 直接发起 HTTP、HTTPS、WebSocket、DNS 或其他网络请求；
- 要求或内置 OpenAI API Key；
- 读取或导出 Codex 原始登录令牌；
- 检查对话、提示词、项目源码、浏览器历史或剪贴板；
- 把额度或账户数据写入磁盘（本地只保存用户选定的刷新间隔）；
- 发送遥测、分析数据、崩溃报告或使用统计；
- 连接广告、追踪或第三方服务；
- 在不同电脑之间传输凭据或账户信息。

完整程序源码只有一个主要文件：[`src/Program.cs`](src/Program.cs)。任何人都可以检查代码，确认程序只使用 Windows Forms、本地进程、JSON 解析和少量 Windows 界面函数。

### 关于“本身不联网”的准确解释

余量条本身不会直接连接互联网。但是，它调用的本机 **Codex** 进程在检查登录状态或获取当前限额时，可能连接 OpenAI。这与用户正常运行 Codex 时的网络行为相同，并受用户自己的 Codex 安装和 OpenAI 账户设置约束。

如果 Codex 无法连接其服务，程序可能无法取得最新额度。因此它不是离线额度计算器，而是本机 Codex 返回数据的轻量显示器。

## 运行要求

### 直接运行 EXE

- Windows 10 或 Windows 11。
- Windows 通常自带的 .NET Framework 4.x。
- Codex CLI 或 VS Code 的 OpenAI Codex 扩展。
- 当前 Windows 用户已经登录 Codex。
- Codex 自身能够访问获取当前账户信息所需的网络。

### 从源码编译

- Windows 10 或 Windows 11。
- .NET Framework 4.x 编译器，或者带 Windows Forms 支持的 Visual Studio / Build Tools。
- 不需要 NuGet 包或第三方代码依赖。

在项目根目录运行：

```bat
build.cmd
```

编译脚本会查找：

```text
%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
```

编译结果位于：

```text
dist/CodexQuotaTray.exe
```

## Codex 查找规则

程序会按以下位置查找 `codex.exe`：

1. 当前 `PATH` 环境变量包含的目录。
2. 当前用户的 VS Code 扩展目录。
3. 当前用户 npm 全局目录中的 `@openai/codex`。

便携版或非标准编辑器目录可能无法自动识别。遇到这种情况，请把 `codex.exe` 所在目录加入 `PATH`。

## 操作方法

- 拖动悬浮标签：改变位置。
- 双击悬浮标签：打开或切换到 Codex。
- 右键悬浮标签：立即刷新、选择 1、3、5 或 10 分钟刷新间隔、打开 Codex 或退出。
- 再次启动 EXE：新进程会立即退出，不会出现第二个悬浮标签。

## 常见问题

### 显示 `Codex Not installed`

请安装 Codex CLI 或 VS Code 的 OpenAI Codex 扩展。如果安装在自定义位置，请把 `codex.exe` 所在目录加入 `PATH`。

### 显示 `Codex Not signed in`

运行 `codex login` 并完成登录，然后在悬浮标签上右键选择 **Refresh now**。

登录状态按 Windows 用户隔离。管理员账户、普通账户和远程桌面账户可能分别拥有不同的登录状态。

### 显示 `Codex Unavailable` 或读取超时

可能原因包括：

- 防火墙、代理或网络问题导致 Codex 无法访问服务；
- Codex 登录已经过期；
- 当前用户无法访问本地 `.codex` 目录；
- Codex 版本不支持程序需要的 app-server 方法；
- Codex 更新后修改了本地协议。

建议运行：

```powershell
codex login status
codex app-server --help
codex update
```

然后重新启动余量条。

### 杀毒软件或 Windows SmartScreen 警告

Release 中的 EXE 可能没有商业代码签名。从网络下载未签名程序时，Windows 可能显示 SmartScreen 警告。你可以先审查源码或自行编译；确认下载来源可信后，也可以选择“更多信息”→“仍要运行”。

企业电脑还可能受到 AppLocker、杀毒软件或终端安全策略限制，需要管理员放行。

1.3.0 版移除了强制结束子进程、进程枚举和高频置顶轮询，并加入标准的 `asInvoker` 应用清单及 Windows 版本信息，以降低启发式误报概率。任何未签名 EXE 都无法保证不被所有杀毒引擎误判；获得受信任的代码签名证书才是建立长期信誉的最佳方式。

### 双击后没有打开 Codex

程序会先尝试切换到已经运行的 Codex 窗口。如果没有找到，则执行：

```powershell
codex app
```

如果当前 CLI 不支持 `app` 命令，请更新 Codex。

### 被其他程序遮挡

程序只在窗口显示、移动或激活状态变化时重新确认置顶，不再高频轮询。使用管理员权限的应用和少数独占全屏程序仍可能遮挡普通权限窗口。通常不建议为了悬浮显示而长期使用管理员身份运行本工具。

### 多显示器或显示缩放

悬浮标签启动时会吸附到鼠标所在屏幕的可用工作区右下角，因此不会被任务栏盖住。紧凑字体会参考 Windows 桌面图标文字设置并适配当前显示 DPI，之后仍可拖到任意位置。

## 仓库文件说明

| 文件 | 用途备注 |
| --- | --- |
| `.codex-plugin/plugin.json` | Codex 插件清单和界面元数据。 |
| `.gitignore` | 避免把生成的二进制、压缩包和本地 IDE 文件提交到源码仓库。 |
| `LICENSE` | 具有法律效力的英文 MIT 许可证。 |
| `LICENSE.zh-CN.md` | 方便阅读的非官方简体中文译本；如有差异，以英文原文为准。 |
| `README.md` | GitHub 默认英文说明、安全说明、安装方法和故障排查。 |
| `README.zh-CN.md` | 与英文 README 对应的简体中文说明。 |
| `NOTES.txt` | 与发布版 EXE 放在一起的英文快速说明。 |
| `NOTES.zh-CN.txt` | 与发布版 EXE 放在一起的简体中文快速说明。 |
| `build.cmd` | 使用 .NET Framework C# 编译器的无第三方依赖编译脚本。 |
| `launch-quota-tray.vbs` | 不显示控制台窗口地启动编译后程序。 |
| `stop-quota-tray.cmd` | 停止正在运行的余量条进程。 |
| `src/Program.cs` | 正式源码，包含 Codex 查找、本地 IPC、界面和单实例逻辑。 |
| `src/AssemblyInfo.cs` | 写入 EXE 的标准产品、发布者、版权和版本信息。 |
| `src/app.manifest` | 声明普通非管理员权限及 DPI 感知的 Windows 应用。 |
| `skills/codex-quota-tray/SKILL.md` | Codex 插件用于启动和停止工具的 Skill 指令。 |
| `dist/CodexQuotaTray.exe` | 生成的发布程序；由 `.gitignore` 排除，不建议提交到源码分支。 |

## 兼容性

额度读取依赖本机 Codex app-server 协议。该协议可能随 Codex 版本变化，建议在 Codex 大版本升级后重新测试。

Codex Quota Tray 1.3.2 基于 Codex CLI `0.142.0` 开发和验证。

## 许可证

本项目使用 MIT 许可证。具有法律效力的条款见 [`LICENSE`](LICENSE)，简体中文参考译文见 [`LICENSE.zh-CN.md`](LICENSE.zh-CN.md)。

## 免责声明

本项目是独立开发的工具，并非 OpenAI 官方产品。“OpenAI”和“Codex”是其各自所有者的商标。
