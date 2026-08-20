# Rhombus.WinFormsMcp

<p align="center"><strong>让 AI 看见、操作、理解并验证 Windows Forms 应用</strong></p>

<p align="center">
  <a href="README.zh-CN.md">中文</a> · <a href="README.md">English</a>
</p>

<p align="center">
  <a href="https://github.com/Lateautumns/winforms-mcp/actions/workflows/ci.yml"><img src="https://github.com/Lateautumns/winforms-mcp/actions/workflows/ci.yml/badge.svg" alt="CI 状态"></a>
  <a href="https://opensource.org/licenses/MIT"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="MIT 许可证"></a>
</p>

WinForms MCP 是一个通过标准 MCP stdio 协议工作的 Windows Forms 开发辅助服务。它可以让 Claude Code、Cursor、Cline、VS Code 或其他 MCP 客户端启动和操作 WinForms 程序，读取真实控件信息，渲染 Designer 界面，并验证修改结果。

## 快速开始

### 方式一：使用 NPM

目标电脑安装 Windows x64 版 Node.js 20 LTS 或更高版本，然后在 MCP 客户端配置中加入：

```json
{
  "mcpServers": {
    "winforms-mcp": {
      "command": "npx",
      "args": ["-y", "@fnrhombus/winforms-mcp"],
      "env": {
        "HEADLESS": "false",
        "TELEMETRY_OPTOUT": "true"
      }
    }
  }
}
```

`command`、`args` 和 `env` 的写法适用于支持 stdio MCP 的客户端。服务启动后不会在终端显示普通提示，这是等待 MCP 客户端通过标准输入发送请求的正常状态。

### 方式二：使用独立 ZIP

从 [GitHub Releases](https://github.com/Lateautumns/winforms-mcp/releases) 下载经过验证的 ZIP，完整解压后把 `command` 指向 `winformsmcp.exe`：

```json
{
  "mcpServers": {
    "winforms-mcp": {
      "command": "C:/Tools/winforms-mcp/winformsmcp.exe"
    }
  }
}
```

不要只复制 exe。`rendererhost/` 目录必须与 exe 保持原有结构，否则 `winforms_render_form` 无法按目标项目的 TFM 加载渲染进程。

### 方式三：从源码运行

适用于 Fork、RC 或本地二次开发：

```powershell
git clone https://github.com/Lateautumns/winforms-mcp.git
cd winforms-mcp
dotnet restore
dotnet run --project src/Rhombus.WinFormsMcp.Server -c Release
```

源码构建要求 Windows x64 和 .NET 8 SDK；运行服务建议安装 .NET 8 Windows Desktop Runtime。源码构建不会自动发布 NuGet、NPM 或 GitHub Release。

## AI 能做什么

| 能力 | 说明 |
| --- | --- |
| 运行程序 | 启动、附加、关闭进程，读取 PID、窗口和状态 |
| UI Automation | 查找控件、点击、输入、选择、拖放、截图 |
| Managed Control 检查 | 通过 RuntimeBridge 读取真实 `Control.Controls` 树、属性、布局、Binding 和 HWND |
| 源码关联 | 从运行中的控件定位 Designer 声明、初始化代码、事件处理器和完整符号 |
| 视觉验证 | 渲染 `.Designer.cs`，或对运行窗口截图并比较修改前后结果 |
| 诊断 | 检查布局、DPI、可访问性并跟踪受限 WinForms 事件 |
| AntdUI 语义 | 读取 Button、Input、Table、Tree、Tabs 等控件的语义信息 |

当前服务注册 46 个 `winforms_*` 工具。工具名称、输入字段和输出结构以 [MCP API 文档](docs/MCP-API.zh-CN.md) 为准。

## 推荐使用流程

让 AI 按下面的顺序工作，可以减少盲目修改：

1. 使用 `winforms_launch_app` 或 `winforms_attach_to_process` 连接目标程序。
2. 使用 `winforms_runtime_status` 判断目标程序是否启用了 RuntimeBridge。
3. 使用 `winforms_get_control_tree` 理解真实的 Managed Control 层级。
4. 使用 `winforms_inspect_control`、`winforms_get_bindings` 和 `winforms_get_layout` 检查属性和布局。
5. 使用 `winforms_get_source_mapping` 定位 `.Designer.cs`、事件处理器和完整符号。
6. 由 AI 的终端或 IDE 工具修改源码并构建项目。
7. 使用 `winforms_render_form`、`winforms_take_screenshot` 或诊断工具验证结果。

## RuntimeBridge

业务 WinForms 项目可以引用 `Rhombus.WinFormsMcp.RuntimeBridge`，在 UI 线程启动只读桥接：

```csharp
using Rhombus.WinFormsMcp.RuntimeBridge;

form.Shown += (_, _) => McpRuntimeBridge.StartForControl(form);
form.FormClosed += (_, _) => McpRuntimeBridge.Stop();
```

RuntimeBridge 通过带版本的 Named Pipe 返回快照，不跨进程序列化 `Control`、`Form` 或 `Binding` 对象；所有 WinForms 属性读取都会切换到 UI 线程。未接入 RuntimeBridge 时，原有 UIA 工具仍可正常使用。

详细接入方式、环境变量、客户端配置和故障排查请阅读：[中文配置与 AI 使用指南](docs/Chinese-Configuration-and-AI-Usage.md)。

## 渲染与兼容性

`winforms_render_form` 会在独立 RendererHost 进程中按目标框架渲染：

| 目标项目 | RendererHost |
| --- | --- |
| .NET Framework 4.0–4.8.x | `net48` |
| .NET Core 3.x | `netcoreapp3.1` |
| .NET 5、6、7、8、9+ | `net8.0-windows` |

支持标准 WinForms，并提供 AntdUI Provider 和语义检查。第三方控件 DLL 必须能够被目标渲染进程解析。

## 权限边界

WinForms MCP **不是任意 Shell 执行器**：

- MCP 工具负责进程、UIA、截图、渲染和只读运行时检查；
- AI 客户端自己的终端权限负责执行 `dotnet build`、测试和 Git 命令；
- RuntimeBridge 只返回运行时快照，不提供任意反射执行或任意属性写入；
- MCP Server 默认通过 stdio 通信，不监听网络端口。

## 文档

- [中文文档索引](docs/Chinese-Documentation-Index.md)
- [中文配置与 AI 使用指南](docs/Chinese-Configuration-and-AI-Usage.md)
- [中文 MCP API](docs/MCP-API.zh-CN.md)
- [中文发布架构](docs/architecture/Release-Architecture.zh-CN.md)
- [中文兼容性矩阵](docs/release/Compatibility-Matrix.zh-CN.md)
- [中文发布检查清单](docs/release/v1.0.0-rc1-checklist.zh-CN.md)
- [English README](README.md)

## 许可证

[MIT License](LICENSE)
