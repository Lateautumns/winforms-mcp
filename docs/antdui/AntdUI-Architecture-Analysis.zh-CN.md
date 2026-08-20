# AntdUI 架构分析

[返回中文文档索引](../Chinese-Documentation-Index.md) · [English 原文](AntdUI-Architecture-Analysis.md)

阶段：第 2 阶段源码侦察。

范围：对本地 AntdUI 参考源码进行只读分析。本文档不实现 AntdUI Provider（提供程序），也不会将 AntdUI 作为编译期依赖引入 RuntimeContracts、RuntimeBridge 核心或 Server 核心。

参考源码根目录：`D:\06_开源工具重写\AntdUIAntdUI`

## 源码证据

以下分析源自这些源码文件：

- `src/AntdUI/Controls/IControl.cs`
- `src/AntdUI/Controls/Button.cs`
- `src/AntdUI/Controls/Input/Input.cs`
- `src/AntdUI/Controls/InputNumber.cs`
- `src/AntdUI/Controls/Checkbox.cs`
- `src/AntdUI/Controls/Radio.cs`
- `src/AntdUI/Controls/Switch.cs`
- `src/AntdUI/Controls/Select.cs`
- `src/AntdUI/Controls/Tabs/Tabs.cs`
- `src/AntdUI/Controls/Tabs/Tabs.Design.cs`
- `src/AntdUI/Controls/Tree.cs`
- `src/AntdUI/Controls/Menu.cs`
- `src/AntdUI/Controls/Table/Table.cs`
- `src/AntdUI/Controls/Table/Table.Data.cs`
- `src/AntdUI/Controls/Table/Table.Layout.cs`
- `src/AntdUI/Controls/Table/Table.Filter.cs`
- `src/AntdUI/Controls/Table/Table.Template.cs`
- `src/AntdUI/Forms/BaseForm.cs`
- `src/AntdUI/Forms/Window.cs`
- `src/AntdUI/Lib/Config.cs`
- `src/AntdUI/Lib/ThemeConfig.cs`
- `src/AntdUI/Lib/Helper/Helper.DPI.cs`

## 继承关系

大多数 AntdUI 控件通过 AntdUI 的 `IControl` 基类成为真正的 WinForms 控件：

```text
System.Windows.Forms.Control
        -> AntdUI.IControl
        -> AntdUI.Button
        -> AntdUI.Checkbox
        -> AntdUI.Radio
        -> AntdUI.Switch
        -> AntdUI.Tree
        -> AntdUI.Menu
        -> AntdUI.Table
        -> AntdUI.Tabs
```

重要特例：

- `AntdUI.IControl : Control, BadgeConfig`
- `AntdUI.Button : IControl, IButtonControl, IEventListener`
- `AntdUI.Input : IControl, IEventListener`
- `AntdUI.InputNumber : Input`
- `AntdUI.Select : Input, SubLayeredForm`
- `AntdUI.Menu : IControl, SubLayeredForm, IEventListener`
- `AntdUI.Table : IControl, IEventListener, IScrollBar`
- `AntdUI.Tree : IControl, IEventListener, IScrollBar`
- `AntdUI.BaseForm : Form`
- `AntdUI.Window : BaseForm, IMessageFilter`

Provider 层面含义：首个 AntdUI Provider 可以通过 `control.GetType().FullName` 以及遍历基类型查找 `AntdUI.IControl` 来识别控件。它不应要求 RuntimeBridge 核心直接引用 AntdUI。

## IControl 公共运行时模型

`IControl` 是主要的共享运行时表面：

- 它继承 `System.Windows.Forms.Control`，因此创建时拥有正常的 HWND。
- 它重写了 `Visible`，并通过 `InvokeRequired` 提供线程感知的设置行为。
- 它将 `ColorScheme` 暴露为 `TAMode`，默认值为 `Auto`。
- 它暴露 `Dpi`，该值首先遵循 `Config._dpi_custom`，然后使用 `BaseForm.Dpi`、`ILayeredForm.Dpi` 或屏幕 DPI。
- 它使用 `Config.TouchEnabled`、`Config.TouchThreshold`、`Config.MouseHoverDelay`、`Config.TouchClickEnabled` 以及动画设置。
- 它暴露 `RenderRegion` 和绘制事件，但这些与图形处理相关、开销较大，默认不应读取。

Provider 层面含义：`IControl` 提供了良好的通用 AntdUI 身份、主题、DPI 和形状基线。Provider 应优先读取公开的标量和字符串属性，除非明确要求，否则避免读取图形对象。

## 控件摘要

| 控件 | 运行时类型 | 基类型 | 复合 | 内部模型 | 分层窗口 | 设计器 |
|---|---|---|---|---:|---:|---:|
| Button | `AntdUI.Button` | `IControl` | 否 | 中 | 否 | 标准 |
| Input | `AntdUI.Input` | `IControl` | 否 | 高 | 上下文菜单/历史记录 | 标准 |
| InputNumber | `AntdUI.InputNumber` | `Input` | 否 | 中 | 继承 Input 行为 | 标准 |
| Checkbox | `AntdUI.Checkbox` | `IControl` | 否 | 低 | 否 | 标准 |
| Radio | `AntdUI.Radio` | `IControl` | 否 | 低 | 否 | 标准 |
| Switch | `AntdUI.Switch` | `IControl` | 否 | 低 | 否 | 标准 |
| Select | `AntdUI.Select` | `Input` | 否 | 高 | 是 | 标准 |
| Tabs | `AntdUI.Tabs` | `IControl` | 逻辑页面 | 高 | 否 | 自定义设计器 |
| Tree | `AntdUI.Tree` | `IControl` | 逻辑节点 | 高 | 否 | 内容序列化 |
| Table | `AntdUI.Table` | `IControl` | 逻辑行/单元格 | 非常高 | 筛选弹窗 | 内容与隐藏状态 |
| Menu | `AntdUI.Menu` | `IControl` | 逻辑菜单项 | 高 | 是 | 内容序列化 |

## 安全的公共属性组

以下属性对 Provider 而言价值高、风险低，因为它们是公共 API，且大多是标量或类似 DTO：

- 通用 WinForms：`Name`、`Text`、`Bounds`、`ClientSize`、`Visible`、`Enabled`、`Handle`、`Dock`、`Anchor`、`Padding`、`Margin`、`Font`、`ForeColor`、`BackColor`。
- AntdUI 通用：`ColorScheme`、`Dpi`、`HandCursor`、`HandDragFolder`。
- Button：悬停/激活颜色、图标字段、加载字段、切换字段、`DialogResult`。
- Input：前缀/后缀文本、占位符本地化文本、`MaxLength`、`AcceptsTab`、`AcceptsEscape`、`HideSelection`、`UseContextMenu`、`VirtualMode`、加载字段。
- InputNumber：`Increment`、`AlwaysShowControl`、`InterceptArrowKeys`、`EnabledValueTextChange`，以及通过 InputNumber 公共 API 暴露的数值字段。
- Checkbox/Radio/Switch：选中状态、`AutoCheck`、本地化文本、选中变更事件。
- Select：`Items`、选中值/索引、通过公共 API 暴露的弹出/下拉状态。
- Tabs：页面集合、选中索引/页面、页面文本/启用/可见。
- Tree：项集合、节点文本/名称/id、选中/展开/选中状态、深度和子项集合。
- Table：列、数据源、虚拟模式、选择、筛选、排序状态、渲染行缓存以及模板行/单元格结构。

## 反射分类

仅对无法通过公共 API 获取、且语义检查所必需的数据使用反射：

| 成员区域 | 访问级别 | 含义 | 风险 | 版本敏感性 |
|---|---|---|---|---|
| `IControl.Dpi` | 公共 | 运行时缩放因子 | 低 | 低 |
| `IControl.ColorScheme` | 公共 | 亮/暗/自动意图 | 低 | 低 |
| `Table.rows` / 布局缓存 | 内部字段 | 渲染行和可见单元格 | 中 | 高 |
| `Table.dataTmp` / 行缓存 | 内部字段 | 规范化数据源行 | 中 | 高 |
| `Table.SortHeader` | 内部/公共（视上下文而定）| 排序状态 | 中 | 中 |
| `Table.rowsFilter` / 筛选缓存 | 内部 | 当前筛选结果 | 中 | 高 |
| `LayeredForm*` 类 | 内部 | 弹出/对话框/下拉窗口 | 中 | 高 |
| 图形路径/位图/画布 | 内部/公共 | 绘制状态 | 高 | 高 |

默认的 Provider 行为应避免读取内部字段，除非请求的语义功能无法通过公共 API 构建。每次反射读取都应尽力而为、按成员隔离，并返回警告，而不是让整个检查失败。

## 不安全或应避免的读取

默认情况下应避免读取以下内容：

- `Graphics`、`Canvas`、`GraphicsPath`、`Bitmap`、`SafeBitmap`、缓存的阴影图像，或任何会创建绘制资源的属性。
- 会测量文本、捕获控件、渲染位图或调用 `Print` 的 getter 路径。
- 原生包装器和 Win32 结构，除非是 WinForms 或 Server HWND 检查层已提供的稳定 HWND 相关元数据。
- 行/项数量不受限制的集合，除非提供了 `maxNodes`、`start`、`count`、`rowCount` 或等价边界。

## Table 模型

Table 是最复杂的第一波 AntdUI 控件。来自 `Table.cs`、`Table.Data.cs`、`Table.Layout.cs`、`Table.Filter.cs` 和 `Table.Template.cs` 的证据表明：

- `Table : IControl, IEventListener, IScrollBar`。
- 公共配置包括 `VirtualMode`、`MultipleRows`、`DefaultExpand`、`FilterRealTime`、`FilterShowCheckBg`、`FilterSortOrder`、`EditAutoHeight`、`EditLostFocus`，以及许多列/单元格行为设置。
- 数据摄取在 `Table.Data.cs` 中将 `DataSource` 规范化为内部行/缓存结构。
- 筛选在 `Table.Filter.cs` 中使用 `RowsCache`、`rowsFilter` 和筛选匹配。
- 布局在 `Table.Layout.cs` 中构建行模板和可见单元格。
- 单元格取值使用列键和 `PropertyDescriptor.GetValue` 路径。
- 虚拟模式会改变行遍历和可见行的实体化。Provider 必须遵守行限制。

第 2 阶段结论：

- 列来自 `Table.cs` 中的 `Column` 定义。
- 数据行来自经 `Table.Data.cs` 规范化的 `DataSource`。
- 可见/已渲染行来自 `Table.Layout.cs` 中的行模板和行列表缓存。
- 单元格值通过列键和属性描述符或渲染值解析。
- 单元格按钮由表格单元格按钮类和 `Table.Render.Button.cs` 中的渲染代码表示。
- 选中行状态保存在行/模板状态和选择辅助逻辑中；首个实现应只暴露可安全读取的内容。
- 排序状态与 `SortHeader` 以及布局/排序路径绑定。
- 筛选状态与 `Filter` 定义和 `rowsFilter` 绑定。
- 虚拟化通过 `VirtualMode` 显式启用。

## Tree 模型

来自 `Tree.cs` 的 Tree 证据表明：

- `Tree : IControl, IEventListener, IScrollBar`。
- 它拥有 `TreeItemCollection` 和 `TreeItem`。
- `TreeItem` 暴露 ID/名称/文本/本地化字段、tag、深度、子项，以及用于选中/展开/选中行为的状态字段。
- Provider 应暴露受 `maxDepth` 和 `maxNodes` 限制的语义节点。

## Tabs 模型

来自 `Tabs.cs` 和 `Tabs.Design.cs` 的 Tabs 证据表明：

- `Tabs : IControl, IEventListener`。
- 它拥有逻辑上的 `TabPage` 项，而不是为每个页面语义使用普通子控件。
- `Tabs.Design.cs` 包含 `ParentControlDesigner`、`DesignerActionList`、`IDesignerHost` 以及选择服务集成。
- Provider 应将 `Tabs` 暴露为语义子项：带有文本、启用/可见、选中状态以及（存在时）关联控件的 `TabPage` 节点。

## Select 模型

来自 `Select.cs` 和分层窗口类的 Select 证据表明：

- `Select : Input, SubLayeredForm`。
- 它拥有 `SelectItem` 数据以及选中项/索引/值状态。
- 下拉 UI 使用诸如 `LayeredFormSelectDown` 的分层表单类。
- 下拉弹窗不可靠地作为 Select 控件的 `Control.Controls` 子项。它们应通过 HWND/窗口检查发现，并与所属控件关联。

## 主题与 DPI

来自 `Config.cs`、`ThemeConfig.cs`、`Helper.DPI.cs`、`BaseForm.cs`、`Window.cs` 和 `IControl.cs` 的证据表明：

- `TAMode` 用于表示控件配色方案意图，例如 Auto/亮/暗行为。
- `Config` 拥有全局运行时行为，例如动画、阴影、触摸、悬停延迟和自定义 DPI。
- `IControl.Dpi` 可从 `Config._dpi_custom`、`BaseForm.Dpi`、`ILayeredForm.Dpi` 或屏幕 DPI 推导。
- `Window` 继承 `BaseForm`，并参与自定义窗口行为。

Provider 层面含义：主题和 DPI 应作为运行时快照暴露。检查工具不得修改全局 `Config` 状态。

## 设计器兼容性

设计器证据：

- 许多控件使用诸如 `ToolboxItem` 的常规 WinForms 特性。
- `Tabs.Design.cs` 包含带有 `ParentControlDesigner`、`DesignerActionList` 和 `IDesignerHost` 的自定义设计器支持。
- 一些集合使用 `DesignerSerializationVisibility.Content`；一些运行时属性被隐藏。

渲染器层面含义：`DesignSurfaceFormRenderer` 必须为自定义设计器元数据和集合序列化做好准备，但第 2 阶段不修改渲染器行为。

## 第 3 阶段的 Provider 设计说明

- 添加一个 Provider 注册表，能够优先选择 `StandardWinFormsProvider`，并在 `FullName` 或基类型匹配 `AntdUI.*` 时选择 `AntdUIProvider`。
- 在 RuntimeBridge 核心中仅通过反射进行 AntdUI 检测，或将 AntdUI 特定逻辑放入单独的 Provider 程序集，避免将 AntdUI 引用强制引入核心项目。
- 保持语义树调用有界。Table、Tree、Tabs、Menu 和 Select 必须支持分页或 `maxNodes`。
- RuntimeBridge 保持只读。不得调用属性 setter、执行任意反射或调用方法。
- 所有运行时控件读取继续使用 UI 线程调度。
