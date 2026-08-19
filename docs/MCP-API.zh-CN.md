# WinForms MCP API（v1 冻结）

[返回中文文档索引](Chinese-Documentation-Index.md) · [English 原文](MCP-API.md)

本文档冻结了 `v1.0.0-rc1` 验证线上的 MCP 工具名称、必填输入、输出封装与兼容性
规则。发现的事实来源是 `ToolDefinitionCatalog`，执行的事实来源是相应的处理程序。
共有 **46 个工具**。工具名称是下面展示的完整 `winforms_*` 字符串；示例中的短
名称不是被接受的别名。

## 公共协议与结果规则

- 服务器通过 stdio 使用官方的 Model Context Protocol .NET SDK。
- 每个成功的结构化结果都包含 `success: true` 以及为该工具列出的载荷。
- 失败的调用是一个带有结构化内容的 MCP 错误结果：

  ```json
  {
    "success": false,
    "error": {
      "code": "...",
      "message": "...",
      "exceptionType": "...",
      "retryable": false,
      "elapsedMs": 12
    }
  }
  ```

- 注册表应用已配置的 `TOOL_TIMEOUT_MS`（默认 30,000 毫秒），关联调用方的
  `CancellationToken`，记录耗时，并返回 `timeout` 或 `cancelled`，而不是泄漏异常。
- 成功时，`imageBase64` 会被转换为 MCP 图像内容；结构化结果会省略 base64 字段。
  其他载荷以 JSON 文本和结构化内容返回。
- 请求中省略的可选字段使用处理程序默认值。未知字段会被已发布的输入架构拒绝。
- RuntimeBridge 是可选的。当目标未暴露桥接时，运行时工具会返回结构化的桥接错误；
  所有 UIA 工具在没有它的情况下仍可正常工作。
- 运行时标识以 `pid` 和 `bridgeInstanceId` 为作用域。运行时和诊断工具接受可选的
  `bridgeInstanceId`；传入来自 `winforms_runtime_status` 或托管快照的值以拒绝过期
  引用。

表格中的记号：`required` 列出必填属性；`optional` 列出接受的属性及其 JSON 类型。
`object` 和 `array` 载荷指的是当前实现返回的 DTO，而非活动的 WinForms 对象。

## 进程

| 工具 | 说明 | 输入架构 | 输出架构 | 示例 | 兼容性说明 |
| --- | --- | --- | --- | --- | --- |
| `winforms_launch_app` | 启动一个应用程序并返回其进程 ID。 | required: `path:string`; optional: `arguments:string`, `workingDirectory:string` | `{success,pid,processName}` | `{"path":"C:/apps/App.exe"}` | 将进程缓存在当前服务器会话中。 |
| `winforms_attach_to_process` | 通过 PID 或进程名称附加。 | required: neither; optional: `pid:integer`, `processName:string` (one is required at runtime) | `{success,pid,processName}` | `{"pid":1234}` | 附加作用于本机/UI 会话范围。 |
| `winforms_close_app` | 优雅地或强制地关闭。 | required: `pid:integer`; optional: `force:boolean` | `{success,message}` | `{"pid":1234,"force":false}` | `force:true` 会终止进程；仅用于验证进程。 |
| `winforms_get_process_status` | 读取进程状态、退出码、响应性、标题和 stderr。 | required: `pid:integer` | `{success,...status fields}` | `{"pid":1234}` | 状态字段来源于进程服务，进程退出后可能为 null。 |

## UI 自动化发现与检查

| 工具 | 说明 | 输入架构 | 输出架构 | 示例 | 兼容性说明 |
| --- | --- | --- | --- | --- | --- |
| `winforms_find_element` | 查找一个 UIA 元素并缓存它。 | optional: `automationId:string`, `name:string`, `className:string`, `controlType:string`, `parent:string` | `{success,elementId,name,automationId,controlType}` | `{"name":"MainForm"}` | 返回会话本地的 UIA ID；进程重启后需重新获取。 |
| `winforms_find_elements` | 查找所有匹配的 UIA 元素并缓存它们。 | same filters as `find_element` | `{success,count,elements:[{elementId,name,automationId,controlType}]}` | `{"controlType":"Button"}` | 结果不是托管控件 ID。 |
| `winforms_element_exists` | 检查 AutomationId。 | required: `automationId:string` | `{success,exists:boolean}` | `{"automationId":"btnSave"}` | 启用时使用有界 UIA 工作器，否则回退到进程内。 |
| `winforms_wait_for_element` | 等待 AutomationId 出现。 | required: `automationId:string`; optional: `timeoutMs:integer` | `{success,exists:boolean,elapsedMs}` | `{"automationId":"grid","timeoutMs":5000}` | 同时受 `timeoutMs` 和服务器工具超时约束。 |
| `winforms_get_element_tree` | 返回有界 UIA 树并缓存返回的元素。 | optional: `pid:integer`, `elementId:string`, `depth:integer`, `maxElements:integer` | `{success,tree,elementCount}` | `{"pid":1234,"depth":3,"maxElements":100}` | 这是 UIA 树；要获取托管 Control 请使用 `get_control_tree`。 |
| `winforms_get_property` | 读取 UIA 属性或模式值。 | required: `elementId:string`, `propertyName:string` | `{success,propertyName,value}` | `{"elementId":"elem_1","propertyName":"Name"}` | 读取 UIA，而非托管的 `TypeDescriptor` 属性。 |
| `winforms_get_table_data` | 从 UIA 网格/表格读取分页数据。 | required: `elementId:string`; optional: `startRow:integer`, `rowCount:integer`, `columns:integer[]` | Service result plus `success` (typically `rows`, `columns`, counts) | `{"elementId":"elem_2","rowCount":20}` | 提供程序支持程度不一；对于托管表格请使用 AntdUI 语义检查。 |
| `winforms_get_focused_element` | 返回并缓存获得焦点的 UIA 元素。 | optional: `pid:integer` | `{success,elementId,name,automationId,controlType}` | `{"pid":1234}` | 需要桌面/UIA 焦点上下文。 |
| `winforms_list_windows` | 列出顶层和从属的 UIA 窗口。 | required: `pid:integer` | `{success,windowCount,windows:[...windowIndex]}` | `{"pid":1234}` | 这是 UIA/窗口服务数据；`get_window_tree` 会增加托管/HWND 元数据。 |
| `winforms_listen_for_event` | 等待一个 UIA 事件。 | optional: `elementId:string`; required: `eventType:string`; optional: `timeoutMs:integer` | `{success,fired,eventDetails,elapsedMs}` | `{"eventType":"WindowOpened","timeoutMs":3000}` | 事件名称是 UIA 事件名称，不是 RuntimeBridge 事件跟踪名称。 |
| `winforms_read_tooltip` | 读取缓存元素的工具提示文本。 | required: `elementId:string` | `{success,tooltip}` | `{"elementId":"elem_3"}` | 工具提示的可用性取决于目标控件和桌面状态。 |
| `winforms_wait_for_condition` | 轮询 UIA 属性比较。 | required: `elementId:string`, `propertyName:string`, `expectedValue:string`; optional: `comparison:string`, `timeoutMs:integer` | `{success,matched,actualValue,elapsedMs}` | `{"elementId":"elem_1","propertyName":"Name","expectedValue":"Ready"}` | 比较方式：`equals`、`contains`、`not_equals`、`greater_than`、`less_than`。 |

## UI 自动化操作

| 工具 | 说明 | 输入架构 | 输出架构 | 示例 | 兼容性说明 |
| --- | --- | --- | --- | --- | --- |
| `winforms_click_element` | 点击一个缓存的 UIA 元素。 | required: `elementId:string`; optional: `doubleClick:boolean` | `{success,message}` | `{"elementId":"elem_1"}` | 尽可能基于模式；回退输入可能需要可见桌面。 |
| `winforms_type_text` | 通过键盘模拟输入文本。 | required: `elementId:string`, `text:string`; optional: `clearFirst:boolean` | `{success,message}` | `{"elementId":"elem_2","text":"NGUS2","clearFirst":true}` | 需要可见桌面/输入模拟。 |
| `winforms_set_value` | 设置 UIA ValuePattern 的值。 | required: `elementId:string`, `value:string` | `{success,message}` | `{"elementId":"elem_2","value":"NGUS2"}` | 当提供程序暴露 ValuePattern 时可在隐藏桌面上工作。 |
| `winforms_drag_drop` | 将一个缓存元素拖到另一个元素。 | required: `sourceElementId:string`, `targetElementId:string` | `{success,message}` | `{"sourceElementId":"elem_1","targetElementId":"elem_2"}` | 仅限可见桌面。 |
| `winforms_send_keys` | 发送 `SendKeys` 语法。 | required: `keys:string`; optional: `pid:integer` | `{success,message}` | `{"keys":"^S","pid":1234}` | 焦点/输入副作用作用于桌面范围。 |
| `winforms_select_item` | 按文本或从零开始的索引选择。 | required: `elementId:string`; optional: `value:string`, `index:integer` | `{success,message}` or provider result | `{"elementId":"elem_4","value":"Online"}` | 需要受支持的选择模式/提供程序。 |
| `winforms_click_menu_item` | 导航并点击菜单路径。 | required: `menuPath:string[]`; optional: `pid:integer` | `{success,message}` | `{"menuPath":["File","Open"]}` | 菜单标签按顺序匹配。 |
| `winforms_toggle_element` | 切换复选框/单选按钮/开关状态。 | required: `elementId:string`; optional: `desiredState:string` | `{success,message}` or state result | `{"elementId":"elem_5","desiredState":"on"}` | `desiredState`：`on`、`off` 或 `indeterminate`。 |
| `winforms_scroll_element` | 使用 UIA ScrollPattern 滚动。 | required: `elementId:string`, `direction:string`; optional: `amount:integer`, `scrollType:string` | `{success,message}` or scroll result | `{"elementId":"elem_6","direction":"down","amount":3}` | `direction`：`up`、`down`、`left`、`right`；`scrollType`：`line` 或 `page`。 |
| `winforms_set_table_cell` | 设置 UIA 网格单元格的值。 | required: `elementId:string`, `row:integer`, `column:integer`, `value:string` | Service result plus `success` | `{"elementId":"elem_7","row":0,"column":2,"value":"Online"}` | 网格/提供程序支持程度不一。 |
| `winforms_manage_window` | 最小化/最大化/还原/移动/调整大小/显示/隐藏/聚焦窗口。 | required: `pid:integer`, `action:string`; optional: `width:integer`, `height:integer`, `x:integer`, `y:integer` | Service result plus `success` | `{"pid":1234,"action":"restore"}` | 坐标和可见性属于 Windows 桌面操作。 |
| `winforms_raise_event` | 在缓存元素上调用受支持的操作。 | required: `elementId:string`, `eventName:string` | `{success,message}` or action result | `{"elementId":"elem_1","eventName":"Invoke"}` | 仅限服务批准的操作；不是任意方法调用。 |
| `winforms_open_context_menu` | 打开上下文菜单并缓存其根。 | required: `elementId:string` | `{success,menuElementId}` | `{"elementId":"elem_1"}` | 弹出窗口的生命周期是短暂的；关闭后需重新获取。 |

## 渲染

| 工具 | 说明 | 输入架构 | 输出架构 | 示例 | 兼容性说明 |
| --- | --- | --- | --- | --- | --- |
| `winforms_render_form` | 在不构建目标项目的情况下渲染 Designer 文件。 | required: `designerFilePath:string`; optional: `outputPath:string`, `theme:string`, `dpi:integer`, `providerProfile:string` | `{success,imageBase64,renderProfile:{theme,dpi,providerProfile,...}}` plus MCP image content | `{"designerFilePath":"C:/src/MainForm.Designer.cs","theme":"dark","dpi":120}` | RendererHost 将 .NET Framework 映射到 `net48`，将 .NET Core 3.x 映射到 `netcoreapp3.1`，将 .NET 5+ 映射到 `net8.0-windows`。 |
| `winforms_take_screenshot` | 捕获进程窗口或缓存元素。 | optional: `pid:integer`, `elementId:string`, `outputPath:string` | `{success,imageBase64}` plus MCP image content | `{"pid":1234,"outputPath":"C:/temp/ngus.png"}` | 服务恰好选择一个目标；MCP 转换后图像数据会从结构化内容中省略。 |

## 运行时检查

运行时工具要求目标进程引用并启动 `Rhombus.WinFormsMcp.RuntimeBridge`。所有读取都会
被封送到 WinForms UI 线程，只有快照 DTO 会跨命名管道传输。

| 工具 | 说明 | 输入架构 | 输出架构 | 示例 | 兼容性说明 |
| --- | --- | --- | --- | --- | --- |
| `winforms_runtime_status` | 检查桥接的可用性和能力。 | required: `pid:integer` | `{success,status:{available,connected,protocolVersion,process,capabilities,bridgeInstanceId,error}}` | `{"pid":1234}` | 安全的首选调用；桥接缺失是一种正常的结果状态。 |
| `winforms_get_control_tree` | 返回有界的托管 `Control.Controls` 树。 | required: `pid:integer`; optional: `rootId:string`, `maxDepth:integer`, `maxNodes:integer`, `bridgeInstanceId:string` | `{success,roots:[ControlTreeNode],nodeCount,truncated,maxDepth,maxNodes}` | `{"pid":1234,"maxDepth":5,"maxNodes":300}` | 托管 ID 以进程/桥接为作用域；树摘要有意比检查更小。 |
| `winforms_inspect_control` | 读取标识、状态、安全属性、布局、绑定、提供程序、语义数据以及 UIA 关联。 | required: `pid:integer`, `controlId:string`; optional: `sections:string[]`, `includeProperties:string[]`, semantic paging (`maxDepth`,`maxNodes`,`start`,`count`,`startRow`,`rowCount`,`rowScope`), `bridgeInstanceId:string` | `{success,identity,summary,state,properties,layout,bindings,correlation[,provider][,semantic]}` | `{"pid":1234,"controlId":"ctrl_18","sections":["identity","layout","semantic"]}` | 属性 getter 默认采用白名单；单个 getter 的失败会出现在 `properties.errors` 中。 |
| `winforms_get_ancestors` | 首先返回最近的托管父级。 | required: `pid:integer`, `controlId:string`; optional: `bridgeInstanceId:string` | `{success,controlId,ancestors:[ControlAncestorSnapshot]}` | `{"pid":1234,"controlId":"ctrl_18"}` | 桥接重启后祖先 ID 会过期；请使用实例 ID。 |
| `winforms_get_window_tree` | 返回有界的 HWND/从属/弹出树以及提供程序弹出元数据。 | required: `pid:integer`; optional: `maxNodes:integer`, `maxItems:integer`, `bridgeInstanceId:string` | `{success,windows:[WindowSnapshot],windowCount}` | `{"pid":1234,"maxNodes":200,"maxItems":100}` | AntdUI 分层窗口是短暂的，枚举期间可能会消失；警告会在元数据中返回。 |
| `winforms_get_bindings` | 读取 `Control.DataBindings`。 | required: `pid:integer`, `controlId:string`; optional: `bridgeInstanceId:string` | `{success,controlId,bindings:[ControlBindingSnapshot],bindingCount}` | `{"pid":1234,"controlId":"ctrl_18"}` | 只读；不执行更新、替换或强制同步。 |
| `winforms_get_source_mapping` | 将托管控件映射到 Designer 位置和事件处理程序符号。 | required: `pid:integer`, `controlId:string`; optional: `sourceRoot:string`, `maxFiles:integer`, `bridgeInstanceId:string` | `{success,mapping:{control,declaration,initialization,designer,namespace,type,fullyQualifiedType,codeBehindFile,events,source,index,warnings}}` | `{"pid":1234,"controlId":"ctrl_18","sourceRoot":"D:/src/NGUS2"}` | 使用有界的 Roslyn 索引。绝对文件路径保持规范形式；相对元数据用于 VS MCP/CodeGraph 交接。 |

## 诊断

| 工具 | 说明 | 输入架构 | 输出架构 | 示例 | 兼容性说明 |
| --- | --- | --- | --- | --- | --- |
| `winforms_get_clipboard` | 读取 Windows 剪贴板文本。 | none | `{success,text}` | `{}` | 需要可访问的剪贴板/UI 会话。 |
| `winforms_set_clipboard` | 写入 Windows 剪贴板文本。 | required: `text:string` | `{success,message}` | `{"text":"NGUS2"}` | 会修改当前用户的剪贴板；仅用于隔离的验证步骤。 |
| `winforms_detect_layout_issues` | 检测有界的布局、DPI 和绑定问题。 | required: `pid:integer`; optional: `rootId:string`, `checks:string[]`, `maxDepth:integer`, `maxNodes:integer`, `maxDiagnostics:integer`, `bridgeInstanceId:string` | `{success,diagnostics:{processId,bridgeInstanceId,diagnostics,checks,scannedNodes,maxNodes,maxDiagnostics,truncated}}` | `{"pid":1234,"checks":["layout","dpi"],"maxNodes":300}` | 仅基于证据进行诊断；此 API 不会自动修复或编辑 Designer 代码。 |
| `winforms_compare_screenshot` | 确定性的有界 PNG 像素差异对比。 | optional: exactly one of `beforePath:string`/`beforeBase64:string` and exactly one of `afterPath:string`/`afterBase64:string`; optional: `maxRegions:integer`, `pixelThreshold:integer` | `{success,diff:{...pixel counts/regions/bounds...}}` | `{"beforePath":"C:/temp/before.png","afterPath":"C:/temp/after.png"}` | 不会推断控件语义；请与运行时/源码检查配合进行诊断。 |
| `winforms_check_accessibility` | 检查托管可访问性元数据和 UIA 模式。 | required: `pid:integer`; optional: `rootId:string`, `maxDepth:integer`, `maxNodes:integer`, `maxDiagnostics:integer`, `bridgeInstanceId:string` | `{success,accessibility:{processId,controls,diagnostics,scannedNodes,maxNodes,maxDiagnostics,truncated}}` | `{"pid":1234,"maxNodes":200}` | UIA 关联是尽力而为的，可用时会包含方法/置信度。 |
| `winforms_start_event_trace` | 启动有界的只读 RuntimeBridge 事件跟踪。 | required: `pid:integer`; optional: `rootId:string`, `events:string[]`, `maxEvents:integer`, `durationMs:integer`, `maxNodes:integer`, `bridgeInstanceId:string` | `{success,trace:{traceId,processId,bridgeInstanceId,active,startedAtUtc,expiresAtUtc,maxEvents,subscribedControlCount,subscribedEvents,nextSequence,...}}` | `{"pid":1234,"events":["Click","TextChanged"],"durationMs":10000}` | 仅订阅文档化的 WinForms 事件白名单。 |
| `winforms_read_event_trace` | 从跟踪会话读取新事件。 | required: `pid:integer`, `traceId:string`; optional: `afterSequence:integer`, `maxEvents:integer`, `bridgeInstanceId:string` | `{success,trace:{events,nextSequence,droppedEventCount,truncated,...}}` | `{"pid":1234,"traceId":"trace_1","afterSequence":0}` | 使用返回的 `nextSequence` 作为下一个游标；跟踪会过期，并且是有界的环形缓冲区。 |
| `winforms_stop_event_trace` | 停止跟踪并分离处理程序。 | required: `pid:integer`, `traceId:string`; optional: `bridgeInstanceId:string` | `{success,trace:{active:false,...}}` | `{"pid":1234,"traceId":"trace_1"}` | 在清理期间调用是安全的；过期的实例 ID 会被拒绝。 |

## RuntimeBridge 与源码兼容性

可选的桥接包在契约边界上有意保持框架中立：

- `Rhombus.WinFormsMcp.RuntimeContracts`：`netstandard2.0` DTO 和 Protocol v1。
- `Rhombus.WinFormsMcp.RuntimeBridge`：`net48` 和 `net8.0-windows`。
- 桥接必须从目标应用程序的 UI 线程启动，并且应在窗体关闭期间停止。它只返回
  快照。
- 像 NGUS2 这样的 `net472` 应用程序在不进行兼容性实验的情况下无法引用当前的
  `net48` 桥接。UIA 自动化和渲染不需要桥接，仍是回退路径。
- 本文档中的任何工具都不授权编辑 Designer 代码、调用 VS MCP、调用 CodeGraph MCP、
  任意反射或更改运行时属性。

## 冻结兼容性策略

对于 v1.x，更改工具名称、必填输入、输出含义或现有字段的语义解释都属于破坏性
变更。仅当旧响应仍然有效时，才允许新增可选字段。新工具需要来自真实工作流的
证据以及相应的 API 评审；默认情况下它们不属于 RC 验证的一部分。
