# AntdUI Provider 映射

[返回中文文档索引](../Chinese-Documentation-Index.md) · [English 原文](AntdUI-Provider-Mapping.md)

阶段：第 2 阶段源码侦察。

本文档将本地 AntdUI 源码模型映射到未来的 Provider（提供程序）设计。它只是文档，不实现 AntdUIProvider。

## 检测策略

RuntimeBridge 核心不应在编译期引用 AntdUI。AntdUI 检测可通过反射完成：

- 运行时类型命名空间以 AntdUI 开头。
- 基类型链包含 AntdUI.IControl。
- 弹窗或瞬时窗口的基类/接口名称包含 ILayeredForm、ILayeredShadowForm、ILayeredFormOpacity、ILayeredFormAnimate 或已知的 LayeredForm* 运行时名称。

Provider 应在 WinForms UI 线程上运行所有控件读取，并仅返回 DTO 快照。

## 控件映射

| 控件 | 运行时类型 | 语义类型 | 公共属性 | 反射成员 | 语义子项 | 特殊状态 | 分层 | 风险 | 优先级 |
|---|---|---|---|---|---|---:|---|---:|
| Button | AntdUI.Button | button | Text、Icon、IconSvg、Loading、LoadingSvg、AutoToggle、Toggle、DialogResult、ColorScheme、Dpi | 避免渲染内部实现 | 无 | loading、toggle、hover、active | 否 | 低 | 1 |
| Input | AntdUI.Input | textbox | Text、PlaceholderText/本地化、前缀/后缀 text/svg、MaxLength、ReadOnly、Multiline、AcceptsTab、UseContextMenu、VirtualMode、ColorScheme、Dpi | 仅在请求时读取文本样式集合 | 可选逻辑文本行 | caret、selection、scroll | 可能有上下文菜单/历史记录 | 中 | 1 |
| InputNumber | AntdUI.InputNumber | number-input | Input 属性、Value、Minimum、Maximum、Increment、AlwaysShowControl、InterceptArrowKeys | 避免微调按钮布局内部实现 | 微调按钮作为语义操作 | 数值 | 继承 Input 行为 | 中 | 1 |
| Checkbox | AntdUI.Checkbox | checkbox | Text、Checked、AutoCheck、ColorScheme、Dpi | 默认无 | 无 | checked | 否 | 低 | 1 |
| Radio | AntdUI.Radio | radio | Text、Checked、AutoCheck、ColorScheme、Dpi | 默认无 | 通过托管扫描得到的父单选组 | checked | 否 | 低 | 1 |
| Switch | AntdUI.Switch | switch | Checked、Text、ColorScheme、Dpi | 默认无 | 无 | checked/loading | 否 | 低 | 1 |
| Select | AntdUI.Select | select | Items、SelectedIndex、SelectedItem、SelectedValue、Text、ColorScheme、Dpi | 仅当公共路径不足时读取弹窗表单引用 | 选择项；打开时的下拉项 | 选择、打开弹窗 | 是 | 中 | 1 |
| Tabs | AntdUI.Tabs | tabs | Items/页面、SelectedIndex、SelectedPage、Text、Enabled、Visible | 运行时避免设计器服务 | 标签页 | 选中页面 | 否 | 中 | 1 |
| Tree | AntdUI.Tree | tree | Items、TreeItem ID/Name/Text/Tag、Checked、Expanded、Selected、Depth | 仅在请求边界时读取布局/滚动缓存 | 树节点 | checked、expanded、selected | 否 | 中 | 1 |
| Table | AntdUI.Table | table | Columns、DataSource、VirtualMode、MultipleRows、筛选/排序配置、选择 | dataTmp、rows、RowsCache、rowsFilter、SortHeader；仅在有界时读取 | 表头、行、单元格、单元格按钮 | 排序、筛选、选择、虚拟化 | 筛选弹窗 | 高 | 1 |
| Menu | AntdUI.Menu | menu | Items、MenuItem text/icon/enabled/visible | 仅在需要时读取分层下拉状态 | 菜单项、按钮、分隔线 | selected、expanded | 是 | 中 | 2 |

## 默认属性策略

首个 Provider 应使用严格的允许列表：

- RuntimeBridge 已捕获的通用 WinForms 属性：Name、Text、Bounds、ClientSize、Visible、Enabled、Handle、Dock、Anchor、Padding、Margin、Font、ForeColor、BackColor。
- 通用 AntdUI：ColorScheme、Dpi、HandCursor。
- Button：Icon、IconSvg、Loading、LoadingSvg、LoadingRespondClick、AutoToggle、Toggle、ToggleText、DialogResult。
- Input：PlaceholderText、PrefixText、PrefixSvg、SuffixText、SuffixSvg、MaxLength、ReadOnly、Multiline、AcceptsTab、AcceptsEscape、HideSelection、UseContextMenu、VirtualMode。
- InputNumber：Value、Minimum、Maximum、Increment、AlwaysShowControl、InterceptArrowKeys。
- Checkbox/Radio/Switch：Checked、AutoCheck。
- Select：Items、SelectedIndex、SelectedItem、SelectedValue。
- Tabs：Items、SelectedIndex、SelectedPage。
- Tree：Items、选中节点、选中与展开节点元数据。
- Table：Columns、DataSource 类型、VirtualMode、MultipleRows、FilterRealTime。

每个 getter 都应隔离。getter 失败时应返回逐属性的错误，而不是让 inspect_control 整体失败。

## 语义子项

| 语义类型 | 子项 | 边界 | 分页 |
|---|---|---:|---:|
| tabs | 标签页 | 可用时的已渲染标签页头部边界 | 页面数量大时：是 |
| tree | 树节点 | 布局缓存可用时的节点边界 | 是 |
| table | 表头、可见行、单元格、单元格按钮 | 是 | 是 |
| menu | 菜单项、按钮、分隔线 | 可见时的项边界 | 是 |
| select | 项与打开的下拉项 | 打开时的下拉项边界 | 是 |

推荐默认值：

- maxDepth：树形控件为 4。
- maxNodes：语义树为 200。
- startRow 和 rowCount：表格为 0 和 50。
- includeOffscreen：除非明确要求，否则为 false。

## Table Provider 计划

Table Provider 应分层实现：

1. 公共摘要：类型、名称、边界、列数、数据源类型、虚拟模式、选择摘要。
2. 列摘要：键、标题、宽度、只读、筛选、排序、固定标志。
3. 可见行摘要：布局缓存可用时的有界行模板。
4. 单元格摘要：列键、显示值、（安全时的）原始值、语义单元格类型。
5. 单元格操作：基于表格单元格类和渲染元数据的 button、checkbox、radio 和 switch 单元格。

如果内部反射失败，返回带警告的降级公共摘要。

## Tree Provider 计划

树节点应作为有界语义节点返回，包含：

- semanticType: tree-node。
- id、name、text、（安全时的）tag。
- depth、childCount。
- checked、expanded、selected。
- 可用时的 bounds。
- 达到 maxDepth 或 maxNodes 时的 truncated 标志。

优先使用公共项集合。除非调用方请求边界，否则避免滚动/渲染缓存。

## Tabs Provider 计划

Tabs 应将标签页暴露为语义子项：

- semanticType: tab-page。
- 文本/本地化文本。
- selected、enabled、visible。
- 页面索引。
- 如果托管子项中存在关联控件，则包含这些控件。

Tabs.Design.cs 包含 ParentControlDesigner、DesignerActionList、IDesignerHost 和选择服务。运行时检查不得调用设计器服务。

## Select Provider 计划

关闭状态下的 Select 应来自 Select 控件本身：

- items。
- 选中索引/值/项。
- text。
- bounds 及启用/可见状态。

打开的下拉状态应来自关联的 LayeredFormSelectDown 或相关分层表单。不要假设下拉项位于 Control.Controls 中。

## 风险与防护措施

- AntdUI 内部成员名称对版本敏感。
- Table 内部实现开销大，必须有界。
- 分层窗口是独立表单/窗口，可能在检查期间消失。
- RuntimeBridge 保持只读：不得调用属性 setter、调用方法、修改全局 Config、修改筛选或修改选择。
- 反射访问必须集中管理，并在第 3 阶段由测试覆盖。
