# 自主开发进度

[返回中文文档索引](../Chinese-Documentation-Index.md) · [English 原文](AUTONOMOUS_PROGRESS.md)

## 阶段

阶段 12 —— 稳定版发布准备工作已在 `feature/v20-release-prep` 上完成本地与 Windows Core CI 门禁；草稿 PR #8 已准备好供人工审查。

## 已实现

- 在 feature/v11-foundation-refactor 上完成了 PR #1 阶段 0 RuntimeBridge 生命周期加固。
- 确认 PR #1 Core CI 在提交 bd19b0ea49a50441d6dbb8f7c75fba61f3d3f652 上为绿色。
- 从 feature/v11-foundation-refactor 创建了堆叠开发分支 feature/v14-antdui-provider。
- 确认 AntdUI 参考仓库位于 D:\06_开源工具重写\AntdUIAntdUI。
- 完成阶段 2 AntdUI 源码侦察文档：
  - docs/antdui/AntdUI-Architecture-Analysis.md。
  - docs/antdui/AntdUI-Provider-Mapping.md。
  - docs/antdui/AntdUI-LayeredWindow-Analysis.md。
- 新增 RuntimeBridge 控件提供程序架构：
  - IControlProvider。
  - IControlProviderRegistry。
  - 基于优先级解析的 ControlProviderRegistry。
  - StandardWinFormsProvider 回退。
- 新增可选的 RuntimeContracts 提供程序与语义快照。
- 新增基于反射的 AntdUIProvider，用于基础 AntdUI 控件：
  - Button。
  - Input。
  - InputNumber。
  - Checkbox。
  - Radio。
  - Switch。
  - Select。
- 新增 Rhombus.WinFormsMcp.AntdUI.TestApp，用于真实的 RuntimeBridge E2E 覆盖。
- 新增 AntdUI 提供程序单元测试与 AntdUI RuntimeBridge 集成测试。
- 通过现有 inspect_control 协议新增对 AntdUI Tabs、Tree、Table 和 Menu 的有界语义检查。
- 新增顶层集合与表格行的语义分页控件（start/count/startRow/rowCount/rowScope），并附带截断元数据。
- 新增 AntdUI Table 列、data/visible/rendered 行作用域、sort/filter 元数据、单元格值及 CellButton 快照。
- 向 AntdUI TestApp 新增复杂语义树夹具和端到端覆盖。
- 新增 AntdUI 分层表单的只读提供程序窗口元数据：
  - Select 下拉框。
  - Menu 弹出框。
  - Tooltip。
  - 兼容 Modal 的分层表面。
  - Drawer。
  - 兼容 Message 和 notification 的分层表面。
- 新增有界弹出项快照、selected/highlighted 状态、可见范围、content/target 边界、DPI、所有者托管身份/路径以及逐窗口警告。
- 为现有 `winforms_get_window_tree` 请求扩展可选 `maxItems`；未新增 MCP 工具。
- 新增真实的 AntdUI 分层窗口 E2E 覆盖，用于 Select、Menu、Tooltip、Message 和 Drawer 的所有者关联与有界元数据。
- 加固了托管控件的 UIA 关联回退，使用自动化 ID、原生 HWND 查找、有界 HWND 遍历和进程匹配。
- 加固了无 ValuePattern 可写控件的 UI 文本输入回退，通过尝试可写子值模式、STA 剪贴板粘贴和限速 SendKeys 回退实现。
- 为现有 `winforms_render_form` 工具扩展可选 `theme`、`dpi` 和 `providerProfile` 字段；阶段 7 工具数量保持为 40。
- 新增请求作用域内的 AntdUI 主题/DPI 反射支持，使用已验证的 `AntdUI.Config.Mode` 和 `Config.SetDpi(float?)` API。
- 在渲染成功和失败后均恢复 AntdUI 全局主题/DPI 状态，包括嵌套的 UserControl 渲染。
- 新增针对主题、DPI、提供程序配置文件、TFM 和引用程序集指纹的渲染缓存隔离。
- 对标准 WinForms 和 AntdUI 预览的 DesignSurface 树应用有界逻辑 DPI 缩放。
- 新增阶段 7 视觉矩阵/状态恢复测试，针对 Light/Dark 96/120/144/192 DPI 下的 AntdUI Button、Input、Tabs、Tree 和 Table 夹具。
- 加固了无 ValuePattern 的自绘/复合控件的 UIA 文本输入，在前台键盘回退之前使用有界、受超时保护的 HWND 键/字符消息；现有 AntdUI 动作 E2E 现已可靠通过。
- 新增共享的 RuntimeContracts 诊断模型，包含显式的严重级别、代码、控件 ID、消息和证据字段。
- 新增源自 UI 线程托管控件快照的有界布局、DPI 和绑定诊断。
- 新增确定性截图比较，支持 PNG/base64 输入、通道阈值、变更边界、有界瓦片区域和取消。
- 新增只读可访问性诊断，包含托管 AccessibleName/Description、TabIndex/TabStop、UIA 关联、ControlType 和支持的模式。
- 新增有界白名单 RuntimeBridge 事件跟踪，用于 Click、TextChanged、CheckedChanged、SelectedIndexChanged、VisibleChanged、EnabledChanged 和 FormClosing，带环形缓冲区、游标分页、过期和处理程序清理。
- 新增用于源码映射的规范根、线程安全增量 `SourceIndex`。
- 索引了命名空间、分部类声明、Designer 字段、`InitializeComponent` 引用、事件注册、处理程序方法和完全限定符号。
- 按路径/大小/UTC 修改时间复用未更改的语法模型，重新解析已更改的文件，移除已删除的文件，并在刷新被取消时保留先前提交的索引。
- 为 `winforms_get_source_mapping` 新增可选 `maxFiles` 和只读扫描元数据（`scanned`、`parsed`、`reused`、`removed`、`truncated` 及解析警告）。
- 记录了来自干净本地参考仓库的已验证 VS MCP 导航/构建/调试契约和 CodeGraph 查询契约。
- 新增可选 `SourceIdentitySnapshot` 交接记录，包含绝对编辑器路径、1-based 跨度、项目/源码根提示、完全限定符号和运行时控件身份。
- 新增可选的正斜杠 `projectRelativeFile` 值用于 CodeGraph 消歧，同时保留所有现有的绝对源码位置字段。
- 新增精确的可选事件处理程序位置，而不更改现有事件的 `file`、`line` 或 `fullyQualifiedSymbol` 字段。
- 将跨 MCP 工作流添加到 README；WinForms MCP 仍不会调用或引用 VS MCP 或 CodeGraph MCP。
- 新增每主机 RuntimeBridge 实例 ID 到 hello/status 和可选请求元数据。
- 为协商客户端新增连接作用域实例验证，同时保留不公布实例 ID 的旧客户端。
- 在 `net48` 和 `net8.0-windows` 上将 RuntimeBridge 管道访问限制为当前 Windows 用户。
- 在 MCP Server 信任 RuntimeBridge 连接之前验证命名管道服务器 PID。
- 用字节有界请求/响应读取器和结构化超大消息错误替换无界行读取。
- 通过发出显式 JSON null 结果保留结构化错误序列化。
- 新增 `Rhombus.WinFormsMcp.UiaWorker`，一个可重启的进程外 UIA2 主机，具有固定的 DTO 命令接口。
- 将根级 `winforms_element_exists` 和 `winforms_wait_for_element` 探测迁移到 worker，而不更改任一工具的契约。
- 新增有界 worker 请求/响应传输、启动/请求超时、超时 Kill、下次调用重建、stderr 诊断和确定性释放。
- 将隐藏桌面自动化保留在现有的桌面感知进程内路径上，并在 worker 二进制文件不可用时新增显式兼容性回退。
- 通过在有界关联窗口内显示之前调用其现有 `NoMessage()` 模式来稳定 AntdUI 分层 Tooltip 测试夹具，使枚举无法关闭该夹具。

## 架构

- RuntimeBridge 仍然是只读检查基础设施。
- RuntimeBridge 核心、RuntimeContracts 和 Server 核心仍无 AntdUI 编译期依赖。
- AntdUI 编译期依赖仅限于 Rhombus.WinFormsMcp.AntdUI.TestApp。
- AntdUIProvider 对白名单公共属性使用受控反射，并具有逐属性错误隔离。
- 提供程序匹配仍集中在 ControlProviderRegistry 中。
- StandardWinFormsProvider 仍是常见 WinForms 控件和未知第三方控件的回退。
- 协议仍为 RuntimeBridge 协议 v1；语义数据通过可选字段添加。
- 窗口快照保留所有现有字段，并新增可选 `providerWindowMetadata`。
- AntdUI 分层窗口发现基于类型身份加上受控反射白名单；它从不调用任意方法或改变弹出状态。
- 提供程序/语义快照通过现有 RuntimeBridge 检查器路径在 WinForms UI 线程上构建。
- 托管 RuntimeBridge 仍是理解层；UIA 仍是操作层。
- 语义读取仍受 RuntimeBridge 钳制及提供程序级集合/行限制约束；非索引偏移以显式元数据故障关闭。
- 渲染仍隔离在 RendererHost 中；未向 Rendering、Server、RuntimeBridge 或 RuntimeContracts 添加 AntdUI 编译期引用。
- 诊断保持通用且与提供程序无关；RuntimeBridge 仍不暴露任何 setter、Method.Invoke、业务方法执行或反射执行面。
- 运行时事件跟踪会话拥有其订阅，具有有界生命周期/容量，并在 Stop、过期、会话压力和主机释放时被移除。
- SourceIndex 状态按规范源码根隔离，按根序列化，有界到固定数量的根/文件，并且从不通过 MCP 暴露 Roslyn 语法对象。
- 跨 MCP 集成仅为元数据层面：未为 VS MCP 或 CodeGraph 添加任何客户端、HTTP 传输、项目引用、包引用或复制的源码。
- RuntimeBridge IPC 仍仅限本地且只读。新客户端协商每实例 nonce；较旧的协议 v1 客户端在同一用户管道 ACL 下保留无 nonce 兼容路径。
- UIA Worker 请求仅包含命令 DTO 和原始 JSON。`AutomationElement`、COM 包装器和实时 UI 对象从不跨越进程边界。

## MCP 变更

- 新增：`winforms_detect_layout_issues`、`winforms_compare_screenshot`、`winforms_check_accessibility`、`winforms_start_event_trace`、`winforms_read_event_trace`、`winforms_stop_event_trace`。
- 变更：`winforms_render_form` 仅新增可选 `theme`、`dpi` 和 `providerProfile` 参数；`winforms_get_source_mapping` 新增可选 `maxFiles`；未更改任何必填参数。
- 扩展：`winforms_get_source_mapping` 新增可选源码身份、项目相对路径和精确的处理程序位置；现有字段保持兼容。
- 扩展：winforms_inspect_control 可通过现有的可选 provider/semantic 段返回 AntdUI 提供程序和语义数据。
- 未变：所有现有 40 个工具名称和必填参数保持兼容；新增的六个是通用诊断，不新增 AntdUI 特定工具。
- 阶段 11 IPC 加固未新增 MCP 工具，也未更改任何必填工具参数。
- 阶段 11 UIA 隔离复用现有 `element_exists` 和 `wait_for_element` 工具；注册表仍为 46 个工具。

## 构建

- 阶段 11 IPC 加固和初始 UIA Worker 本地门禁通过。
- Format：通过。
- Format verify：通过。
- Restore：通过。
- Release 解决方案构建：0 警告，0 错误。
- RendererHost 多目标 Release 构建：0 警告，0 错误。

## 测试

- 本地完整阶段 8 测试运行：共 379 个，335 个通过，44 个跳过，0 个失败（提升桌面会话）。
- 最终夹具重建前本地完整阶段 11 UIA Worker Release 测试运行：共 402 个，358 个通过，44 个跳过，0 个失败。
- 重建分层窗口夹具并连续五轮运行四用例套件：20 个通过，0 个失败。
- 新增覆盖：
  - AntdUI 提供程序检测与回退行为。
  - AntdUI Button、Input、InputNumber、Checkbox、Radio、Switch 和 Select 语义。
  - 带有限 maxNodes 截断的 Select 项语义子项。
  - 通过测试应用的 AntdUI RuntimeBridge E2E。
  - AntdUI 控件的托管/UIA 关联回退。
  - 无直接可写 ValuePattern 控件的 UIA 文本输入回退。
  - AntdUI Tabs 页面选择和有界分页。
  - AntdUI Tree/Menu 层级、选择/状态、深度限制和分页。
  - AntdUI Table 列、行作用域、排序/筛选元数据、行分页和 CellButton 语义。
  - RuntimeBridge 语义选项传输，包括 null 安全 JSON 处理。
  - 现有 MCP 工具面保持兼容；工具注册表总数现为 46 个工具。
  - LayeredWindow 元数据契约序列化和语义分类。
  - Select 下拉项边界/选择/截断和所有者托管 ID。
  - Menu 弹出框、Tooltip、Message 和 Drawer 的 HWND/所有者关联。
  - 渲染视觉选项规范化、缓存隔离、标准 WinForms DPI 缩放。
  - 96/120/144/192 DPI 下的 AntdUI Light/Dark 渲染矩阵以及成功/失败时的全局状态恢复。
  - 布局/DPI/绑定诊断证据和 maxDiagnostics 边界。
  - 确定性截图差异像素、边界、瓦片区域、阈值和无效输入处理。
  - 可访问性诊断边界和托管/UIA 增强。
  - 运行时事件跟踪环形缓冲区、序列分页、过期、Stop 清理和真实 TestApp 命名管道捕获。

## CI

- PR #1 Core CI：绿色。
- PR #1 外部 CI：Claude Code Review 失败，因为 Claude Code GitHub App 未安装在 fork 上。
- 阶段 4 提交前的 PR #2 Core CI：在提交 8d66583 feat: add control provider architecture 上为绿色。
- 阶段 4 提交的 PR #2 Core CI：在提交 b7ac9f2 feat: add AntdUI basic control inspection 上为绿色。
- PR #2 外部 CI：Claude Code Review 因相同的 GitHub App 未安装问题而失败。
- 阶段 4 提交 CI：绿色。
- 阶段 5 Core CI：提交 700adc8 为绿色（push run 32213525287 和 PR run 32213528776）。
- 阶段 6 Core CI：提交 cbc300f 为绿色（push run 32216808052 和 PR run 32216813261）。
- 阶段 6 外部 Claude Code Review：失败，因为 Claude Code GitHub App 未安装在 fork 上；未针对此外部服务故障进行任何代码更改。
- 阶段 7 Core CI：提交 f3bf321 为绿色（push run 32221982955 和 PR run 32221986299）。
- 阶段 7 外部 Claude Code Review：因 Claude Code GitHub App 未安装在此 fork 上而以 401 失败；未针对此外部服务故障进行任何代码更改。
- 阶段 8 Core CI：提交 cd7ef0e 为绿色（push run 32236157363 和 PR run 32236160606）。
- 阶段 8 外部 Claude Code Review run 32236160617 因 GitHub App 未安装在此 fork 上而以 401 失败；未针对此外部服务故障进行任何代码更改。
- 阶段 8 CI 状态提交 c844c1b：Core CI 绿色（push run 32236718177 和 PR run 32236723237）；外部 Claude Code Review run 32236723248 因相同的 App 缺失 401 而失败。
- 阶段 9 CI 状态提交 72ab00f：Core CI 绿色（push run 32243431847 和 PR run 32243436411）；外部 Claude Code Review run 32243436427 因相同的 App 缺失 401 而失败。
- 阶段 10 Core CI：提交 ea615d9 为绿色（push run 32246092879 和 PR run 32246197318）。
- 阶段 10 外部 Claude Code Review run 32246197161 因 Claude Code GitHub App 未安装在此 fork 上而失败；未针对此外部服务故障进行任何代码更改。
- 阶段 11 UIA Worker Windows Core CI：待提交和推送。
- 阶段 11 IPC 加固 Core CI 在提交 `feaf781` 上为绿色：push run `32248894192` 和 PR run `32248925239`。
- 阶段 11 外部 Claude Code Review run `32248925255` 因 GitHub App 未安装在 fork 上而失败；未针对此外部服务故障进行任何代码更改。

## Git

- 基础分支：feature/v17-contract-analysis。
- 当前分支：feature/v18-hardening。
- 阶段 11 IPC 提交：feaf781 `feat: harden runtime bridge ipc security`。
- 草稿 PR：#6 以 `feature/v17-contract-analysis` 为目标，head 为 `feature/v18-hardening`。
- 阶段 7 提交：f3bf321 `feat: support render theme and dpi profiles`。
- 阶段 8 提交：cd7ef0e `feat: add WinForms runtime diagnostics`。
- 阶段 8 CI 状态提交：c844c1b `docs: record stage 8 ci status`。
- 阶段 9 起始提交前的当前 Head：c844c1b。
- 阶段 4 提交：b7ac9f2 feat: add AntdUI basic control inspection。
- 阶段 5 提交：700adc8 feat: add AntdUI complex semantic inspection。
- 阶段 6 提交：cbc300f `feat: support AntdUI layered windows`。
- 草稿 PR：#3 以 feature/v14-antdui-provider 为目标；草稿 PR #4 以 feature/v15-diagnostics 为目标，head 为 `feature/v16-source-index`。
- 草稿 PR #5 以 `feature/v16-source-index` 为目标，head 为 `feature/v17-contract-analysis`。
- 工作树：初始 UIA Worker 隔离切片和 Tooltip 夹具加固通过了最终本地门禁，已准备好提交。

## 风险

- AntdUI 仓库当前包含未跟踪的 .codegraph 目录；将其视为本地分析工件，永不提交。
- AntdUIProvider 有意只读取白名单公共属性和有界项摘要。
- 提供程序实现必须继续避免任意运行时执行、setter 或方法调用。
- 未来的提供程序扩展应保持在现有 provider/semantic 架构内，并避免 AntdUI 特定 MCP 工具，除非兼容性要求如此。
- Table 内部使用 AntdUI 成员的窄白名单，并在版本敏感缓存不可用时返回每作用域回退/诊断元数据。
- 分层表单是瞬态的，可能在枚举期间消失；检查器返回有界元数据和警告并容忍释放竞争。
- 本地 SDK：仓库请求的 .NET 8.0.424 已安装，`global.json` 保持不变。
- 协议 v1 旧客户端可省略实例 ID；同一用户 ACL 和 PID 验证仍然强制执行，而协商客户端在 hello 后需要当前实例 ID。
- UIA 隔离是有意的增量式：根级存在性/等待探测现已隔离；返回或消费缓存的实时 `AutomationElement` 实例的操作仍留在进程内，直到引入定位器/引用再水合。

## 下一步

- 将初始 UIA Worker 隔离切片提交并推送到 PR #6，然后等待 Windows Core CI。
- 继续阶段 11 的多进程运行时身份和剩余的资源生命周期审计。

## 阶段 10 门禁证据

- 参考仓库保持干净/只读：VS-MCPServer `main` 位于 `1d020ae`；CodeGraph `main` 位于 `c6aaa20`。
- `dotnet format Rhombus.WinFormsMcp.sln --verbosity quiet`：通过。
- `dotnet format Rhombus.WinFormsMcp.sln --verify-no-changes --verbosity quiet`：通过。
- `dotnet restore Rhombus.WinFormsMcp.sln`：通过。
- `dotnet build Rhombus.WinFormsMcp.sln --configuration Release --no-restore /m:1 /nr:false`：通过，0 警告 0 错误。
- `dotnet build src/Rhombus.WinFormsMcp.RendererHost/Rhombus.WinFormsMcp.RendererHost.csproj --configuration Release --no-restore /m:1 /nr:false`：对 net48、netcoreapp3.1 和 net8.0-windows 通过，0 警告 0 错误。
- 聚焦的源码身份/源码映射测试：7 个通过。
- 完整 Release 测试运行：共 385 个，341 个通过，44 个跳过，0 个失败。

## 阶段 11 IPC 加固门禁证据

- RuntimeBridge 生命周期/IPC 聚焦测试：20 个通过，0 个跳过，0 个失败。
- `dotnet format Rhombus.WinFormsMcp.sln --verbosity quiet`：通过。
- `dotnet format Rhombus.WinFormsMcp.sln --verify-no-changes --verbosity quiet`：通过。
- `dotnet restore Rhombus.WinFormsMcp.sln`：通过。
- `dotnet build Rhombus.WinFormsMcp.sln --configuration Release --no-restore /m:1 /nr:false`：通过，0 警告 0 错误。
- `dotnet build src/Rhombus.WinFormsMcp.RendererHost/Rhombus.WinFormsMcp.RendererHost.csproj --configuration Release --no-restore /m:1 /nr:false`：对 net48、netcoreapp3.1 和 net8.0-windows 通过，0 警告 0 错误。
- 完整 Release 测试运行：共 391 个，347 个通过，44 个跳过，0 个失败。
- 未新增 MCP 工具；注册表仍为 46 个工具。

## 阶段 11 UIA Worker 门禁证据

- 新增真实 worker 进程并迁移两个根级 UIA 查询路径；未序列化任何 `AutomationElement`。
- 聚焦 UIA Worker/配置/官方 MCP SDK 测试：34 个通过，0 个跳过，0 个失败。
- Worker 生命周期覆盖包括握手、隔离查询、并发复用、超时 Kill/重建、活动请求释放、无头回退和无孤儿进程。
- `dotnet format Rhombus.WinFormsMcp.sln --verbosity quiet`：通过。
- `dotnet format Rhombus.WinFormsMcp.sln --verify-no-changes --verbosity quiet`：通过。
- `dotnet restore Rhombus.WinFormsMcp.sln`：通过。
- `dotnet build Rhombus.WinFormsMcp.sln --configuration Release --no-restore /m:1 /nr:false`：通过，0 警告 0 错误。
- `dotnet build src/Rhombus.WinFormsMcp.RendererHost/Rhombus.WinFormsMcp.RendererHost.csproj --configuration Release --no-restore /m:1 /nr:false`：对 net48、netcoreapp3.1 和 net8.0-windows 通过，0 警告 0 错误。
- 最终夹具重建前的完整 Release 测试重跑：共 402 个，358 个通过，44 个跳过，0 个失败。
- 夹具重建后的最终完整 Release 测试：共 403 个，359 个通过，44 个跳过，0 个失败。
- 在夹具调用 AntdUI 现有 `NoMessage()` 模式后，分层窗口套件在连续五轮中通过 20/20；未削弱任何断言，也未跳过任何测试。

## 阶段 9 范围

- 索引源码根、命名空间、分部类型、Designer 字段和初始化、事件注册、处理程序方法以及完全限定符号。
- 按路径、大小和修改时间缓存每文件解析结果，在时间戳精度不足时使用可选内容哈希。
- 复用未更改的解析结果，仅使已更改/已删除的文件失效。
- 通过最大文件数、取消和现有工具超时管道保持扫描有界。

## 硬阻塞项

无。

## 阶段 9 门禁证据

- 仓库 SDK：.NET 8.0.424；`global.json` 未更改且干净。
- `dotnet format Rhombus.WinFormsMcp.sln --verbosity quiet`：通过。
- `dotnet format Rhombus.WinFormsMcp.sln --verify-no-changes --verbosity quiet`：通过。
- `dotnet restore Rhombus.WinFormsMcp.sln`：通过。
- `dotnet build Rhombus.WinFormsMcp.sln --configuration Release --no-restore /m:1 /nr:false`：通过，0 警告 0 错误。
- `dotnet build src/Rhombus.WinFormsMcp.RendererHost/Rhombus.WinFormsMcp.RendererHost.csproj --configuration Release --no-restore /m:1 /nr:false`：对 net48、netcoreapp3.1 和 net8.0-windows 通过，0 警告 0 错误。
- 聚焦 SourceIndex/源码映射测试：6 个通过。
- 完整 Release 测试运行：共 384 个，340 个通过，44 个跳过，0 个失败。
- Core CI：提交 `5779490` 在 push run `32242797084` 和 pull-request run `32242800459` 上均为绿色。
- 外部 Claude Code Review run `32242800339`：因 Claude Code GitHub App 未安装在 fork 上而以 401 失败；这是非阻塞的，无需代码更改。

## 阶段 6 门禁证据

- `dotnet format Rhombus.WinFormsMcp.sln --verbosity quiet`：通过。
- `dotnet format Rhombus.WinFormsMcp.sln --verify-no-changes --verbosity quiet`：通过。
- `dotnet restore Rhombus.WinFormsMcp.sln`：通过。
- `dotnet build Rhombus.WinFormsMcp.sln --configuration Release --no-restore /m:1 /nr:false`：通过，0 警告 0 错误。
- `dotnet build src/Rhombus.WinFormsMcp.RendererHost/Rhombus.WinFormsMcp.RendererHost.csproj --configuration Release --no-restore /m:1 /nr:false`：对 net48、netcoreapp3.1 和 net8.0-windows 通过，0 警告 0 错误。
- 聚焦 LayeredWindow 和弹出 E2E 测试：18 个通过。
- 完整提升桌面测试运行：共 348 个，304 个通过，44 个跳过，0 个失败。
- 非提升完整测试运行：一个现有 FlaUI `SendInput` 访问被拒失败；提升重跑通过。

## 阶段 5 门禁证据

- dotnet format Rhombus.WinFormsMcp.sln --verbosity quiet: 通过。
- dotnet format Rhombus.WinFormsMcp.sln --verify-no-changes --verbosity quiet: 通过。
- dotnet restore Rhombus.WinFormsMcp.sln: 通过。
- dotnet build Rhombus.WinFormsMcp.sln --configuration Release --no-restore /m:1 /nr:false: 通过，0 警告 0 错误。
- dotnet build src/Rhombus.WinFormsMcp.RendererHost/Rhombus.WinFormsMcp.RendererHost.csproj --configuration Release --no-restore /m:1 /nr:false: 通过，0 警告 0 错误。
- dotnet test Rhombus.WinFormsMcp.sln --configuration Release --no-build: 共 334 个，290 个通过，44 个跳过，0 个失败。
- GitHub PR #2 阶段 4 CI run 32188783782：通过，Windows 上 build-test-coverage 为绿色。
- GitHub PR #2 阶段 5 push run 32213525287：通过，Windows 上 CI 为绿色。
- GitHub PR #2 阶段 5 synchronize run 32213528776：通过，Windows 上 CI 为绿色。
- GitHub PR #2 阶段 5 status push run 32213834322：通过，Windows 上 CI 为绿色。
- GitHub PR #2 阶段 5 status synchronize run 32213836905：通过，Windows 上 CI 为绿色。
- 聚焦 AntdUIProviderTests：7 个通过。
- 聚焦 RuntimeBridgeLifecycleTests：14 个通过。
- 聚焦 RuntimeInspectionTests：4 个通过。
- 一次非提升桌面 E2E 尝试被 Windows SendInput 访问拒绝；相同的 E2E 和完整 Release 运行在提升测试会话中通过。这是环境权限说明，而非产品测试失败。

## 阶段 7 门禁证据

- `dotnet format Rhombus.WinFormsMcp.sln --verbosity quiet`：通过。
- `dotnet format Rhombus.WinFormsMcp.sln --verify-no-changes --verbosity quiet`：通过。
- `dotnet restore Rhombus.WinFormsMcp.sln`：通过。
- `dotnet build Rhombus.WinFormsMcp.sln --configuration Release --no-restore /m:1 /nr:false`：通过，0 警告 0 错误。
- `dotnet build src/Rhombus.WinFormsMcp.RendererHost/Rhombus.WinFormsMcp.RendererHost.csproj --configuration Release --no-restore /m:1 /nr:false`：对 net48、netcoreapp3.1 和 net8.0-windows 通过，0 警告 0 错误。
- 聚焦阶段 7 RuntimeBridge、AntdUI 提供程序、渲染和渲染池测试：33 个通过。
- 完整提升 Release 测试运行：共 369 个，325 个通过，44 个跳过，0 个失败。
- 现有 AntdUI 自绘 Input UIA 动作现在在 ValuePattern 不可用时使用有界 HWND WM_KEY/WM_CHAR 回退通过。

## 阶段 8 门禁证据

- `dotnet format Rhombus.WinFormsMcp.sln --verbosity quiet`：通过。
- `dotnet format Rhombus.WinFormsMcp.sln --verify-no-changes --verbosity quiet`：通过。
- `dotnet restore Rhombus.WinFormsMcp.sln`：通过。
- `dotnet build Rhombus.WinFormsMcp.sln --configuration Release --no-restore /m:1 /nr:false`：通过，0 警告 0 错误。
- `dotnet build src/Rhombus.WinFormsMcp.RendererHost/Rhombus.WinFormsMcp.RendererHost.csproj --configuration Release --no-restore /m:1 /nr:false`：对 net48、netcoreapp3.1 和 net8.0-windows 通过，0 警告 0 错误。
- 完整提升 Release 测试运行：共 379 个，335 个通过，44 个跳过，0 个失败。
- Windows Core CI 对功能提交 cd7ef0e 通过：push run 32236157363 和 pull_request run 32236160606。
- 外部 Claude Code Review run 32236160617 以已知的 GitHub App 缺失 401 失败，且不是 Core CI 故障。

## 阶段 11 运行时身份门禁证据

- 运行时作用域身份现在在托管控件摘要、祖先、分层窗口所有权元数据、源码映射、诊断和事件跟踪快照中携带 `processId` 和 `bridgeInstanceId`。
- 现有运行时/诊断工具保留所有先前的必填参数，并接受一个可选 `bridgeInstanceId`；工具注册表仍为 46 个工具。
- 命名管道客户端对每个运行时请求执行 hello 协商，并在发送命令前拒绝预期的过期实例。省略可选身份的旧客户端保持兼容。
- 运行时身份测试覆盖弱控件生命周期、主机过期实例拒绝、客户端过期实例拒绝、旧回退、并发关闭和输出上下文传播。
- `dotnet format Rhombus.WinFormsMcp.sln --verbosity quiet`：通过。
- `dotnet format Rhombus.WinFormsMcp.sln --verify-no-changes --verbosity quiet`：通过。
- `dotnet restore Rhombus.WinFormsMcp.sln`：通过。
- `dotnet build Rhombus.WinFormsMcp.sln --configuration Release --no-restore /m:1 /nr:false`：通过，0 警告 0 错误。
- `dotnet build src/Rhombus.WinFormsMcp.RendererHost/Rhombus.WinFormsMcp.RendererHost.csproj --configuration Release --no-restore /m:1 /nr:false`：对 net48、netcoreapp3.1 和 net8.0-windows 通过，0 警告 0 错误。
- 聚焦身份/生命周期/检查/源码索引/诊断测试：48 个通过，0 个失败。
- 完整 Release 测试运行：共 406 个，362 个通过，44 个跳过，0 个失败。
- AntdUI 参考仓库保持只读；其预先存在的未跟踪 `.codegraph` 分析工件未被触碰或提交。

## 阶段 12 发布准备范围

- 发布准备仅限本地：包清单、兼容性文档、迁移/发布说明以及可复现的本地包/ZIP 检查是允许的。
- 在无人值守执行期间，不允许 NuGet push、NPM publish、GitHub release 或修改 `main`。
- 兼容性声明将区分本地已验证目标与当前环境中不可用的 Windows/OS 组合。

## 阶段 12 门禁证据

- 门禁时间戳：2026-08-19 21:40:12 +08:00。
- 分支：`feature/v20-release-prep`，基于 `a35e7db` / `feature/v19-runtime-identity`。
- 新增 RuntimeContracts 和 RuntimeBridge 的包元数据和 README 收录。
- 新增仅本地的 `scripts/package-local.ps1` 检查，涵盖三个 NuGet 包、一个 NPM tarball 以及包含所有 RendererHost 目标的独立 ZIP。
- 新增兼容性矩阵、迁移指南、发布说明草稿、发布架构、README 发布准备指引以及 `1.5.12-beta` 变更日志条目。
- `dotnet format Rhombus.WinFormsMcp.sln`：通过。
- `dotnet format Rhombus.WinFormsMcp.sln --verify-no-changes`：通过。
- `dotnet restore Rhombus.WinFormsMcp.sln`：通过。
- `dotnet build Rhombus.WinFormsMcp.sln --configuration Release --no-restore /m:1 /nr:false`：通过，0 警告 0 错误。
- `dotnet build src/Rhombus.WinFormsMcp.RendererHost/Rhombus.WinFormsMcp.RendererHost.csproj --configuration Release --no-restore /m:1 /nr:false`：对 net48、netcoreapp3.1 和 net8.0-windows 通过，0 警告 0 错误。
- 完整 Release 测试运行：共 406 个，362 个通过，44 个跳过，0 个失败。
- `scripts/package-local.ps1 -Configuration Release`：通过；所有预期的 NuGet、NPM 和 ZIP 工件断言均成功，且未发布。
- AntdUI 参考仓库保持只读；未将 `.codegraph` 工件或构建输出添加到 Git。
- 提交：`edce1dd`（`chore: prepare local 1.5.12-beta release`）。
- 草稿 PR：#8，`feature/v20-release-prep` -> `feature/v19-runtime-identity`。
- Windows Core CI：对推送的 head `24a84ec` 为绿色（push run `32259744121`、pull-request run `32259755640`）。
- 外部 Claude Code Review run `32259755616` 因 Claude Code GitHub App 未安装在此 fork 上而以 401 失败；未针对此外部服务故障进行任何代码更改。

## 候选发布版本验证

- 当前阶段：候选发布版本验证，分支 `release/v1.0.0-rc1`，基于 `feature/v20-release-prep` 的 `7bbd2b0`。
- 在 `docs/MCP-API.md` 新增冻结 API 参考，在 `docs/release/v1.0.0-rc1-checklist.md` 新增候选门禁。API 清单与 `ToolNames`/`ToolDefinitionCatalog` 中的 46 个定义匹配。
- 真实项目目标：只读 `D:\02_工作\在研项目\NGUS2`，项目 `NGUS2\NGUS2.csproj`，现有 `NGUSV3.2.exe`、`.NET Framework 4.7.2`、AnyCPU、AntdUI 2.4.x。
- UIA 验证针对发布输出的可丢弃副本通过：attach、进程状态、单窗口枚举、48 节点元素树、真实属性读取、窗口截图、无变化截图差异以及缓存的 UIA 标签页交互。原始业务仓库未被修改。
- RuntimeBridge 状态对 NGUS2 正确降级为结构化不可用错误，因为当前 bridge 目标为 `net48` 和 `net8.0-windows`；此目标的托管树、源码映射和 RuntimeBridge 诊断仍未验证，并记录为兼容性限制。
- 真实 AntdUI 渲染最初暴露了两个渲染器依赖缺口：旧项目直接在 `bin\Release` 中输出程序集，且主应用程序程序集是 `.exe`。`FormRenderingHelpers` 现在考虑直接配置输出、DLL/EXE 程序集，并优先选择最完整的候选目录。一个回归测试覆盖仅 Debug EXE 与 Release DLL/EXE 输出。
- 修复后，`winforms_render_form` 成功以 AntdUI/Light/96 DPI 渲染 NGUS2 `MainForm.Designer.cs`。生成的 PNG 为 30,911 字节，且不再包含 NGUS2 自定义控件的 `Type not found` 占位符。
- 修复后的聚焦回归门禁：`FormRenderingHelpersTests` 13 个通过，0 个失败。
- RC 本地门禁于 `2026-08-19 23:14:16 +08:00` 完成：format 和 verify-no-changes 通过；restore 通过；解决方案 Release 构建以 0 警告/错误通过；RendererHost `net48`、`netcoreapp3.1` 和 `net8.0-windows` 构建以 0 警告/错误通过；完整 Release 测试以共 407 个、363 个通过、44 个跳过、0 个失败通过。
- Windows ZIP 路径断言规范化后，本地包验证通过。它在临时目录中生成了三个 NuGet 包、一个 NPM tarball 和独立 ZIP，未发布。
- 发现的问题：RuntimeBridge TFM 不匹配和嵌套 UIA 桌面查询限制；两者都不足以证明需要更改协议或新增工具。
- 草稿 PR #9 以 `feature/v20-release-prep` 为目标；最终 RC 验证内容的 Windows Core CI 通过（run `32270980161`）。
- 外部 Claude Code Review run `32270980234` 因 Claude Code GitHub App 未安装在此 fork 上而以已知的 401 失败；这是非阻塞的，且未引起代码更改。
- RC 验证已完成。剩余工作是对已记录的 NGUS2 net472 RuntimeBridge 限制进行人工验收，并在任何 `v1.0.0` 标签或包/发布发布之前获得批准。请勿修改 `main`、发布包或触碰 NGUS2/AntdUI 源码仓库。
