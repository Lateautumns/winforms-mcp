# WinForms MCP 中文配置与 AI 使用指南

本文面向需要在另一台 Windows 电脑上安装、配置和使用 WinForms MCP 的开发者，
也可以直接提供给 Claude Code、Cursor、Cline、VS Code MCP 或其他兼容 MCP 的 AI
编程工具阅读。

本文以当前仓库的 MCP Server 为准。MCP Server 通过标准输入/输出（stdio）与 AI
客户端通信，默认不监听网络端口。AI 通过 MCP 工具理解和操作 WinForms 程序；
项目源码、终端命令和编辑器权限仍由 AI 客户端自身提供。

## 1. 先理解它能做什么

WinForms MCP 的定位是让 AI 能够观察、运行、操作和验证 Windows Forms 应用：

| 能力 | 典型用途 |
| --- | --- |
| 进程管理 | 启动、附加、关闭应用，检查 PID、窗口标题和响应状态 |
| UI Automation | 查找控件、读取 UIA 属性、点击、输入、选择、拖放、截图 |
| Managed Control 检查 | 通过可选 RuntimeBridge 读取真实 `Control.Controls` 树、属性、布局、Binding 和 HWND |
| 源码关联 | 从运行中的控件定位 Designer 声明、初始化代码、事件处理器和完整符号 |
| 视觉验证 | 直接渲染 `.Designer.cs`，或对运行窗口截图、比较修改前后的 PNG |
| 诊断 | 布局、DPI、可访问性和受限 WinForms 事件跟踪 |

当前工具名、输入字段和输出结构以 [MCP API 文档](MCP-API.md) 为准。当前注册表
包含 46 个 `winforms_*` 工具。

### 重要的权限边界

WinForms MCP **不是任意 Shell 执行器**。它没有提供“执行任意 `cmd.exe`、PowerShell
或 Python 字符串”的 MCP 工具，也不会自动修改 Designer 文件或调用任意反射方法。

- AI 客户端的终端能力可以执行 `dotnet build`、测试和 Git 命令，这是客户端自己的权限。
- WinForms MCP 负责进程、UIA、截图、渲染和只读运行时检查。
- RuntimeBridge 只返回快照；UIA 工具负责交互动作。
- 需要修改源码时，让 AI 使用 IDE/终端工具修改，然后再用 WinForms MCP 验证结果。

这样可以把“改代码”和“操作运行中的程序”分开，避免把业务进程变成任意反射执行环境。

## 2. 目标电脑的前置条件

### 必需条件

1. Windows x64。NPM 包明确限制为 `win32/x64`，不支持 macOS、Linux 或 Windows ARM64。
2. 一个支持 MCP stdio 的 AI 客户端。
3. 运行服务所需的 .NET 运行时：
   - MCP Server 使用 `net8.0-windows`，建议安装 **.NET 8 Windows Desktop Runtime x64**。
   - 渲染 .NET Framework 项目需要 Windows 的 .NET Framework 4.8 运行环境。
   - 渲染 `netcoreapp3.1` 项目时还需要对应的 .NET Core 3.1 运行时；该版本已停止支持，
     只在确实需要时安装。

### 使用 NPM/npx 时

建议安装 Node.js 20 LTS 或更新的 Windows x64 版本。项目最低声明 Node.js 14，但新电脑
使用 LTS 版本更容易获得稳定的 `npx` 和证书支持。

在 PowerShell 中检查：

```powershell
node --version
npm --version
node -p "process.platform + ' ' + process.arch"
dotnet --list-runtimes
```

最后一条命令应该能看到类似 `Microsoft.WindowsDesktop.App 8.0.x`。如果使用独立 ZIP，
仍建议检查 .NET 8 Windows Desktop Runtime，因为仓库发行包包含应用程序集，不等同于
一个完全自包含的 .NET 运行时安装包。

## 3. 选择安装方式

按下面的优先级选择：

| 场景 | 推荐方式 |
| --- | --- |
| 只想使用已发布版本 | NPM `npx` |
| 不想安装 Node.js | GitHub Release 独立 ZIP |
| 要使用本仓库 RC、Fork 或本地修改 | 从源码 Release 构建并直接运行 `winformsmcp.exe` |
| 需要给业务项目启用 Managed Control 检查 | 额外引用 RuntimeBridge NuGet/项目 |

### 方式 A：NPM/npx

如果目标版本已经发布到 NPM：

```powershell
npx -y @fnrhombus/winforms-mcp
```

`npx` 会下载包并启动 stdio Server。直接在终端执行后没有可见提示、进程持续等待，
通常是正常现象；MCP 客户端会通过标准输入发送握手和工具调用。不要把这个命令当成
普通 CLI 命令等待文本输出。

生产环境或团队环境建议固定已经验证过的版本：

```powershell
npx -y @fnrhombus/winforms-mcp@<已验证版本>
```

当前 RC 分支的版本号是 `1.5.12-beta`，本仓库没有在 RC 阶段自动发布 NPM 包。若 NPM
中找不到该版本，请使用方式 B 或方式 C，不要把未发布的版本号写进配置。

### 方式 B：Release 独立 ZIP

1. 从 [Lateautumns/winforms-mcp Releases](https://github.com/Lateautumns/winforms-mcp/releases) 下载对应 ZIP。
   如果页面没有对应资产，说明当前 RC/测试阶段尚未发布独立包，请改用“方式 C：从本仓库源码构建”，
   或由团队管理员提供经过验证的 ZIP；不要从不明来源下载 `winformsmcp.exe`。
2. 解压到没有特殊权限限制的目录，例如 `C:\Tools\winforms-mcp`。
3. **不要移动或删除子目录**，尤其要保留：

   ```text
   C:\Tools\winforms-mcp\winformsmcp.exe
   C:\Tools\winforms-mcp\rendererhost\net48\
   C:\Tools\winforms-mcp\rendererhost\netcoreapp3.1\
   C:\Tools\winforms-mcp\rendererhost\net8.0-windows\
   ```

4. 在 MCP 配置中使用 `winformsmcp.exe` 的绝对路径。

`render_form` 会根据目标项目的 TFM 选择对应 RendererHost；只复制 exe 而不复制
`rendererhost` 会导致渲染失败。

### 方式 C：从本仓库源码构建

适用于要使用 Fork、RC 分支或还没有发布的修改。目标电脑需要 Git 和 .NET 8 SDK。

```powershell
$repo = "C:\src\winforms-mcp"
git clone https://github.com/Lateautumns/winforms-mcp.git $repo
Set-Location $repo
git fetch --all --prune
git checkout release/v1.0.0-rc1

dotnet restore Rhombus.WinFormsMcp.sln
dotnet build Rhombus.WinFormsMcp.sln --configuration Release --no-restore
dotnet build src/Rhombus.WinFormsMcp.RendererHost/Rhombus.WinFormsMcp.RendererHost.csproj `
  --configuration Release --no-restore
```

构建成功后，推荐直接运行：

```text
C:\src\winforms-mcp\src\Rhombus.WinFormsMcp.Server\bin\Release\net8.0-windows\winformsmcp.exe
```

在配置中使用绝对路径，不要依赖当前工作目录。也可以临时使用 `dotnet run`，但每次
启动会检查项目并可能触发构建：

```json
{
  "command": "dotnet",
  "args": [
    "run",
    "--project",
    "C:/src/winforms-mcp/src/Rhombus.WinFormsMcp.Server/Rhombus.WinFormsMcp.Server.csproj",
    "--configuration",
    "Release",
    "--no-restore"
  ]
}
```

## 4. 配置 MCP 客户端

### 通用 JSON 配置

很多客户端（Claude Desktop、Cline 以及部分 IDE）使用 `mcpServers` 根节点。把下面
的 `command` 和 `args` 放进客户端的 MCP 配置文件：

```json
{
  "mcpServers": {
    "winforms-mcp": {
      "command": "npx",
      "args": ["-y", "@fnrhombus/winforms-mcp"],
      "env": {
        "HEADLESS": "false",
        "TELEMETRY_OPTOUT": "true",
        "TOOL_TIMEOUT_MS": "30000",
        "RUNTIME_BRIDGE_ENABLED": "true",
        "UIA_WORKER_ENABLED": "true"
      }
    }
  }
}
```

使用独立 exe 时改成：

```json
{
  "mcpServers": {
    "winforms-mcp": {
      "command": "C:/Tools/winforms-mcp/winformsmcp.exe",
      "args": [],
      "env": {
        "HEADLESS": "false",
        "TELEMETRY_OPTOUT": "true"
      }
    }
  }
}
```

Windows JSON 中推荐使用 `/`，或者把反斜杠写成 `\\`。例如
`C:\\Tools\\winforms-mcp\\winformsmcp.exe` 是合法 JSON，单个 `C:\Tools` 不是。

如果客户端报告 `npx ENOENT`，把 command 改为 `npx.cmd`，或使用 Node.js 安装目录的
绝对路径，例如 `C:/Program Files/nodejs/npx.cmd`。

### VS Code MCP 配置

VS Code 的 `.vscode/mcp.json` 通常使用 `servers` 根节点，而不是 `mcpServers`：

```json
{
  "servers": {
    "winforms-mcp": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@fnrhombus/winforms-mcp"],
      "env": {
        "HEADLESS": "false"
      }
    }
  }
}
```

如果你的 VS Code 版本提示 schema 不接受 `servers` 或 `type`，以该版本的 MCP 配置
提示为准；服务本身只要求最终以 stdio 启动 `winformsmcp.exe`。

### Claude Code

可以在项目目录放置 `.mcp.json`，让配置随项目一起管理。也可以使用 Claude Code 的
MCP 命令添加项目级服务：

```powershell
claude mcp add --scope project --transport stdio winforms-mcp -- `
  npx -y @fnrhombus/winforms-mcp
```

如果使用本地 exe，把命令后的部分替换为：

```powershell
claude mcp add --scope project --transport stdio winforms-mcp -- `
  C:/Tools/winforms-mcp/winformsmcp.exe
```

添加后重新打开 Claude Code 会话，并检查 MCP 工具列表中出现 `winforms_*` 工具。

### Claude Desktop、Cline、Cursor 等客户端

这些客户端的配置文件路径和 JSON schema 可能随版本变化。使用客户端的 **MCP / Tools /
Servers** 设置页面打开配置，粘贴“通用 JSON 配置”中的 `winforms-mcp` 节点即可。

配置完成后必须完全退出并重新启动客户端；只关闭聊天窗口通常不会重启 stdio 子进程。

## 5. 环境变量怎么配

所有值都以字符串形式放进 MCP 配置的 `env` 对象。没有特殊需求时保留默认值即可。

| 变量 | 默认值 | 什么时候修改 |
| --- | --- | --- |
| `HEADLESS` | `false` | 需要隐藏桌面、不抢焦点时设为 `true` |
| `TFM` | `auto` | RendererHost 自动识别错误时指定 `net48`、`netcoreapp3.1` 或 `net8.0-windows` |
| `TELEMETRY_OPTOUT` | `true` | 默认关闭遥测；只有明确同意时才设为 `false` |
| `LOG_LEVEL` | `Information` | 排查启动问题时可设为 `Debug` 或 `Trace` |
| `TOOL_TIMEOUT_MS` | `30000` | 单个 MCP 调用的总超时，单位毫秒 |
| `RENDERER_TIMEOUT_MS` | `30000` | Designer 渲染请求超时 |
| `RENDERER_STARTUP_TIMEOUT_MS` | `10000` | RendererHost 启动握手超时 |
| `RUNTIME_BRIDGE_ENABLED` | `true` | 不使用 RuntimeBridge 时可设为 `false` |
| `RUNTIME_BRIDGE_CONNECT_TIMEOUT_MS` | `1000` | 连接目标进程 Bridge 的等待时间 |
| `RUNTIME_BRIDGE_REQUEST_TIMEOUT_MS` | `5000` | 单次托管树/属性快照请求等待时间 |
| `UIA_WORKER_ENABLED` | `true` | 是否隔离受支持的 UIA 查询 |
| `UIA_WORKER_PATH` | 自动发现 | 自定义 `Rhombus.WinFormsMcp.UiaWorker.exe` 路径 |
| `UIA_WORKER_STARTUP_TIMEOUT_MS` | `5000` | UIA Worker 握手超时 |
| `UIA_WORKER_REQUEST_TIMEOUT_MS` | `15000` | UIA Worker 单次查询超时 |
| `UIA_WORKER_MAX_RESPONSE_BYTES` | `1048576` | 限制 UIA Worker 响应大小 |

### 可见桌面与 Headless 的选择

- `HEADLESS=false`：适合调试真实界面、键盘输入、拖放和需要焦点的操作。
- `HEADLESS=true`：适合后台测试、避免抢占用户桌面；优先使用 UIA pattern 的
  `winforms_set_value`、`winforms_click_element` 和截图。
- `winforms_send_keys`、`winforms_drag_drop` 依赖可见桌面输入，不要在 headless 模式
  把它们当成可靠操作。

## 6. 第一次连接后的验证顺序

不要一连接就猜控件 ID。让 AI 按下面顺序建立事实：

1. 调用 `winforms_launch_app` 或 `winforms_attach_to_process`，拿到真实 `pid`。
2. 调用 `winforms_get_process_status`，确认进程存在、窗口可见/响应。
3. 调用 `winforms_get_element_tree`，用较小的 `depth` 和 `maxElements` 查看 UIA 树。
4. 调用 `winforms_runtime_status`，确认目标程序是否启用了 RuntimeBridge。
5. 如果 Bridge 可用，调用 `winforms_get_control_tree`，得到真实 managed `Control` 树。
6. 对目标 `controlId` 调用 `winforms_inspect_control`，一次读取 identity、state、layout、
   properties、bindings 和 UIA correlation。
7. 需要定位源码时调用 `winforms_get_source_mapping`，传入绝对 `sourceRoot`。
8. 用 `winforms_take_screenshot` 保存修改前证据；修改代码并构建后再次截图或调用
   `winforms_compare_screenshot`。
9. 完成后调用 `winforms_close_app`，不要默认使用 `force=true`。

所有 `elementId`、`controlId`、`traceId` 都是运行时临时 ID。应用重启或 Bridge 重启后，
必须重新获取树；RuntimeBridge 调用建议同时传回 `bridgeInstanceId`，避免使用过期引用。

### 可复制给 AI 的首次任务提示词

```text
你现在连接了 WinForms MCP。项目根目录是：C:/work/MyWinFormsApp
请遵守以下流程：
1. 先读取 README.md 和 docs/MCP-API.md，确认项目和工具边界。
2. 不要猜 PID、elementId 或 controlId；先 launch/attach，再读取 process status。
3. 先用 winforms_get_element_tree 了解 UIA，再调用 winforms_runtime_status 判断是否有 Managed RuntimeBridge。
4. Bridge 可用时使用 winforms_get_control_tree 和 winforms_inspect_control 理解真实 Control；Bridge 不可用时
   继续使用 UIA，不要声称已经读取 managed 属性。
5. 读取源码时使用绝对 sourceRoot：C:/work/MyWinFormsApp，并通过 winforms_get_source_mapping
   关联 Designer 和事件处理器。
6. 所有树查询设置 maxDepth/maxNodes；输出中说明 truncated、warnings 和证据路径。
7. 修改代码使用当前 AI 客户端的编辑器/终端工具；WinForms MCP 只用于运行时验证。
8. 每次修改后执行构建/测试，再截图或比较截图，最后报告 PID、工具调用、结果和限制。
```

## 7. 让 AI 快速了解“这个仓库是做什么的”

把下面几项一起交给 AI，效果比只说“帮我看看界面”稳定：

```text
仓库根目录：C:/work/winform-mcp
业务项目：C:/work/NGUS2/NGUS2/NGUS2.csproj
运行程序：C:/work/NGUS2/NGUS2/bin/Release/NGUSV3.2.exe
源码根目录：C:/work/NGUS2
UI 框架：标准 WinForms / AntdUI（按实际项目填写）
```

AI 应先阅读：

1. `README.md`：项目目标、安装方式和能力概览。
2. `docs/MCP-API.md`：46 个工具的当前输入/输出契约。
3. 本文：跨电脑配置、权限边界和验证顺序。
4. 业务项目的 `.csproj`、`Program.cs`、Form 的 `.Designer.cs` 和代码后台。

注意：WinForms MCP 能返回源码位置和符号，但不会把整个仓库自动塞进一次工具响应。
让 AI 通过自己的文件读取能力打开源码；用 MCP 返回的绝对路径、行号和
`fullyQualifiedSymbol` 作为导航依据。

## 8. RuntimeBridge：让 AI 看到真实 Control

UIA 只能看到自动化树；如果要读取真实 `Control.Controls`、布局、DataBindings、
自定义 Public Property 和源码映射，需要在目标 WinForms 应用中引用 Bridge。

### 添加引用

目标项目是 `net48` 或 `net8.0-windows` 时，可以引用：

```xml
<ItemGroup>
  <PackageReference Include="Rhombus.WinFormsMcp.RuntimeContracts" Version="<已验证版本>" />
  <PackageReference Include="Rhombus.WinFormsMcp.RuntimeBridge" Version="<已验证版本>" />
</ItemGroup>
```

如果使用当前仓库 RC 尚未发布的包，可临时使用本地项目引用：

```xml
<ProjectReference Include="C:\src\winforms-mcp\src\Rhombus.WinFormsMcp.RuntimeBridge\Rhombus.WinFormsMcp.RuntimeBridge.csproj" />
```

`RuntimeBridge` 当前目标为 `net48;net8.0-windows`。`net472` 应用（例如当前验证的
NGUS2）不能直接引用 `net48` Bridge；这时仍可使用 UIA、截图和 Designer 渲染，
不要为了开启 Bridge 擅自修改业务项目的 TFM。

### 在 UI 线程启动和停止

不要在后台线程直接读取 WinForms 控件。Bridge 内部会把读取调度到 UI 线程，应用只需
在 UI 生命周期中启动和停止：

```csharp
using Rhombus.WinFormsMcp.RuntimeBridge;

private RuntimeBridgeHost? _mcpBridge;

protected override void OnShown(EventArgs e)
{
    base.OnShown(e);

    if (Environment.GetEnvironmentVariable("WINFORMS_MCP_BRIDGE") == "1")
        _mcpBridge = McpRuntimeBridge.Start(new RuntimeBridgeOptions { Debug = true });
}

protected override void OnFormClosed(FormClosedEventArgs e)
{
    McpRuntimeBridge.Stop();
    base.OnFormClosed(e);
}
```

只在开发/调试环境设置 `WINFORMS_MCP_BRIDGE=1`，不要在不了解安全边界时把 Bridge
默认开启到面向用户的生产版本。Bridge 是只读的，但它会暴露运行中的控件结构、属性和
源码关联信息。

## 9. Designer 渲染与第三方控件

调用 `winforms_render_form` 时传入绝对路径，例如：

```json
{
  "designerFilePath": "C:/work/MyWinFormsApp/MainForm.Designer.cs",
  "theme": "Light",
  "dpi": 120,
  "providerProfile": "AntdUI",
  "outputPath": "C:/temp/MainForm-light-125.png"
}
```

渲染不等于构建整个业务项目，但 RendererHost 必须能够加载 Designer 中使用的控件。
对于旧式 `.NET Framework` 项目，请确认：

- `bin/Release` 或 `bin/Debug` 中存在主程序集和依赖 DLL；
- 自定义控件如果输出为 `.exe`，也要保留在输出目录；
- AntdUI 等第三方 DLL 与目标项目版本一致；
- 发行 ZIP 的 `rendererhost/<tfm>` 目录没有被删除；
- 使用 `TFM` 环境变量只作为自动识别失败时的覆盖，不要随意指定错误 TFM。

AI 遇到 `Type not found` 时，应先报告缺少的程序集和目标 TFM，不要直接修改
Designer 文件来掩盖加载错误。

## 10. 常见故障排查

### 客户端显示没有 MCP 工具

1. 确认客户端使用的是 Windows x64 环境。
2. 在 PowerShell 直接运行 `node --version`、`npx --version` 或检查 exe 路径。
3. 把 `command` 改为绝对路径，避免客户端找不到用户 PATH 中的 Node.js。
4. 完全退出并重新启动客户端，确认 MCP 日志中的握手成功。
5. 使用 `npx -y @fnrhombus/winforms-mcp` 时确认目标版本确实存在于 NPM；RC/Fork 用源码 exe。

### `winforms_launch_app` 成功但找不到控件

- 先调用 `winforms_get_process_status` 确认 PID 和窗口响应。
- UIA 树查询使用 `depth`、`maxElements` 限制，先拿根节点再逐层展开。
- 嵌套自定义控件优先使用 `winforms_get_element_tree` 返回的缓存 ID；不要只依赖全桌面搜索。
- headless 模式下不要使用依赖物理键盘/鼠标的 `winforms_send_keys` 和 `winforms_drag_drop`。

### RuntimeBridge 返回 unavailable

- 目标程序没有引用或启动 Bridge；检查应用启动日志和 `WINFORMS_MCP_BRIDGE=1`。
- 目标 TFM 可能是 `net472`；当前 Bridge 不支持直接引用该 TFM。
- Bridge 必须在目标进程内启动，MCP Server 本身的进程 ID 不能当成业务进程 ID。
- 应用重启后要重新获取 `controlId` 和 `bridgeInstanceId`。

### Source Mapping 没有结果

- 给 `winforms_get_source_mapping` 传业务源码根目录的绝对路径 `sourceRoot`。
- 确认控件有稳定的 `Name`，并且 Designer 文件与代码后台在该目录内。
- 先取得 managed `controlId`；没有 Bridge 时不要把 UIA `elementId` 当成 `controlId`。
- 返回的路径和行号用于 AI/VS MCP 导航；服务不会自动修改文件。

### `render_form` 报 RendererHost 或程序集错误

- 检查 .NET 8 Desktop Runtime 和目标 TFM 运行时。
- 检查 exe、DLL、Designer 文件和 `rendererhost` 目录是否来自同一个版本。
- 对旧式项目先执行一次 Release 构建，再把完整输出目录作为依赖来源。
- 设置 `LOG_LEVEL=Debug`，同时保留结构化错误中的 `code`、`message` 和 `elapsedMs`。

### 工具调用超时

先缩小请求：降低 `maxDepth`、`maxNodes`、`rowCount` 或 `maxDiagnostics`，确认不是一次
性返回过大的树。只有在明确需要时才增大 `TOOL_TIMEOUT_MS`、`RENDERER_TIMEOUT_MS` 或
`UIA_WORKER_REQUEST_TIMEOUT_MS`；不要用无限等待掩盖卡死的 UIA Provider。

## 11. 推荐的团队配置约定

1. 把 `.mcp.json` 或 `.vscode/mcp.json` 放在项目仓库，路径尽量使用环境变量或团队约定的
  绝对路径，不要提交个人用户名目录。
2. 正式团队固定 NPM/ZIP 版本；升级前阅读 [MCP API 冻结文档](MCP-API.md) 并重新跑一次
  启动、UIA、渲染和截图验证。
3. 生产环境保持 `TELEMETRY_OPTOUT=true`，仅在明确批准后启用遥测。
4. 只给 AI 访问它确实需要的业务项目目录；MCP 配置本身不应包含密码、Token 或连接字符串。
5. 让 AI 在每次任务报告中列出：PID、使用的工具、截图路径、源码路径/行号、构建结果、
   未验证的限制和是否关闭了测试进程。

## 12. 一次完整的 AI 开发闭环

```text
AI 读取 README / 项目源码
        |
        v
winforms_launch_app 或 attach_to_process
        |
        v
process_status -> element_tree / runtime_status
        |
        +--> UIA 操作：click / set_value / screenshot
        |
        +--> Managed 检查：control_tree -> inspect_control -> source_mapping
        |
        v
AI 使用编辑器/终端修改代码并执行 build/test
        |
        v
render_form 或 take_screenshot / compare_screenshot
        |
        v
AI 输出验证结果和剩余限制
```

建议给 AI 的实际任务描述：

```text
请检查 C:/work/MyWinFormsApp 的设备管理页面。
先不要修改代码：启动或附加到 C:/work/MyWinFormsApp/bin/Release/MyApp.exe，
读取进程状态、UIA 树和 RuntimeBridge 状态；如果 Bridge 可用，找到名为
"logPanel" 的控件并读取 layout、bindings 和 source mapping。
然后给出右侧日志区域高度的修改建议。只有在我确认后才编辑代码；编辑后构建、
重新启动应用、截图并报告修改前后差异。整个过程中不要 force 关闭无关进程。
```

完成配置后，AI 应该能回答“仓库是做什么的、当前窗口有哪些控件、某个按钮对应哪段
源码、修改后布局是否变化”等问题；它仍然会明确报告没有 Bridge、没有源码路径或没有
可见桌面时的限制，而不是编造结果。

## 13. 相关文档

- [MCP API 冻结文档](MCP-API.md)
- [RuntimeBridge 迁移指南](release/Migration-Guide.md)
- [兼容性矩阵](release/Compatibility-Matrix.md)
- [发布架构](architecture/Release-Architecture.md)
- [项目 README](../README.md)
