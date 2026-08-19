# AntdUI 分层窗口分析

[返回中文文档索引](../Chinese-Documentation-Index.md) · [English 原文](AntdUI-LayeredWindow-Analysis.md)

阶段：第 2 阶段源码侦察。

本文档记录 AntdUI 弹窗、下拉、覆盖层和瞬时 UI 表面如何影响 RuntimeBridge 和 HWND 检查。本文档仅为文档。

## 源码证据

分层和弹窗行为出现在以下文件中：

- src/AntdUI/Forms/LayeredWindow/ILayeredForm.cs
- src/AntdUI/Forms/LayeredWindow/ILayeredShadowForm.cs
- src/AntdUI/Forms/LayeredWindow/LayeredFormSelectDown.cs
- src/AntdUI/Forms/LayeredWindow/LayeredFormSelectMultiple.cs
- src/AntdUI/Forms/LayeredWindow/LayeredFormSelectMultipleCheck.cs
- src/AntdUI/Forms/LayeredWindow/LayeredFormMenuDown.cs
- src/AntdUI/Forms/LayeredWindow/LayeredFormContextMenuStrip.cs
- src/AntdUI/Forms/LayeredWindow/LayeredFormModal.cs
- src/AntdUI/Forms/LayeredWindow/LayeredFormDrawer.cs
- src/AntdUI/Controls/Tooltip/Tooltip.Form.cs
- src/AntdUI/Controls/Message.cs
- src/AntdUI/Controls/Notification.cs
- src/AntdUI/Controls/Drawer.cs
- src/AntdUI/Controls/Modal.cs
- src/AntdUI/Controls/Select.cs
- src/AntdUI/Controls/Menu.cs

## 主要分层类型

| 特性 | 运行时类型 | 基类或接口 | 所有者模式 | 说明 |
|---|---|---|---|---|
| Select 下拉 | LayeredFormSelectDown | ILayeredShadowForm, SubLayeredForm | Select/Input 所有者 | 用于下拉内容的独立弹窗表单 |
| Select 多选 | LayeredFormSelectMultiple 和 LayeredFormSelectMultipleCheck | 内部 Select 分层表单 | SelectMultiple 所有者 | 带复选框状态的项模型 |
| Menu 下拉 | LayeredFormMenuDown | ILayeredShadowForm, SubLayeredForm | Menu 所有者 | 独立菜单弹窗 |
| 上下文菜单 | LayeredFormContextMenuStrip | ILayeredShadowFormOpacity, SubLayeredForm | Control/上下文所有者 | 不是所有者的子控件 |
| 工具提示 | TooltipForm | ILayeredFormOpacity, ITooltip | 目标控件或矩形 | 坐标来自目标矩形和屏幕 |
| 模态框 | LayeredFormModal | Window, IEventListener, LayeredFormAsynLoad | 配置目标/表单 | 对话框或无模态覆盖层 |
| 抽屉 | LayeredFormDrawer | ILayeredForm, LayeredFormAsynLoad | 配置表单/内容 | 承载内容并处理遮罩 |
| 消息 | MessageFrm | ILayeredFormAnimate | 全局/消息配置 | 瞬时动画表面 |
| 通知 | NotificationFrm | ILayeredFormAnimate | 全局/通知配置 | 瞬时通知表面 |

## 运行时行为

分层窗口并不是源控件 Control.Controls 集合的可靠后代。它们是具有自己 HWND 和渲染生命周期的独立表单或类表单窗口。

观察到的行为：

- Select 实现 SubLayeredForm，因此下拉 UI 与 Select 控件关联但相互独立。
- Menu 实现 SubLayeredForm，并使用菜单下拉分层表单。
- TooltipForm 根据目标控件或目标矩形计算放置位置。
- LayeredFormDrawer 可以临时承载用户内容、订阅所有者表单的移动和尺寸变化，并在关闭时恢复或释放内容。
- LayeredFormModal 派生自 Window，并且可以拥有嵌套控件。
- 阴影表单会在内容周围添加阴影内边距。HWND 外部边界和内容边界可能不同。
- 分层表单维护自己的 Dpi，并且通常从所有者控件/表单推导。

## 对 HWND 和托管树的影响

RuntimeBridge 托管控件树通常能看到所有者控件，但如果弹窗是独立表单，则可能看不到活动弹窗。因此：

- 托管树应将所有者控件作为稳定标识。
- HWND 树应枚举弹窗、对话框、抽屉、工具提示、消息和通知。
- Provider（提供程序）关联应尽可能将弹窗 HWND/表单链接到所有者托管标识。
- 仅当存在关联的分层表单时，语义树才可包含合成子项，例如 dropdown(open)。

推荐的关联方法：

1. 相同进程 ID。
2. 存在时的所有者或父 HWND 关系。
3. 目标矩形与所有者控件屏幕边界的重叠或邻近。
4. 运行时类型名称，例如 LayeredFormSelectDown。
5. 可从运行时对象获取的 AntdUI SubLayeredForm 关系。

## 建议的快照

建议的弹窗快照字段：

- semanticType: popup。
- provider: AntdUI。
- runtimeType。
- ownerControlId。
- hwnd。
- bounds。
- contentBounds。
- visible。
- dpi。
- 项数量及有界项。
- truncated。
- warnings。

对于阴影表单，在可用时同时暴露外部 HWND 边界和内容边界。这可以避免由阴影内边距导致的错误布局诊断。

## 特性说明

### Select

关闭状态应来自 Select 控件。打开状态应来自 LayeredFormSelectDown 或相关的 Select 分层表单。项遍历必须有界。

### Menu

Menu 数据来自 Menu 和 MenuItemCollection。活动下拉表面应作为 LayeredFormMenuDown 发现。分隔线和自定义按钮项应成为语义节点。

### Tooltip

工具提示的位置由目标矩形驱动。将其表示为瞬时弹窗元数据，而不是所有者控件的普通子项。

### Modal

LayeredFormModal 派生自 Window，因此现有 HWND 树应将其识别为真正的窗口。内容可能是文本、控件内容或自定义内容。RuntimeBridge 不得执行回调。

### Drawer

抽屉可以将 Control 作为内容承载，并可能临时移动或包装它。检查必须只读，且不得强制抽屉打开或关闭。

### 消息与通知

MessageFrm 和 NotificationFrm 是瞬时的动画分层表单。尽可能通过 HWND 树加上运行时类型关联来检测它们。

## 防护措施

- 不得从 RuntimeBridge 检查中调用 show、close、toggle、focus 或激活方法。
- 不得修改所有者表单的激活、topmost、DPI、主题、内容、弹窗状态、选中值、筛选或表格状态。
- 容忍检查期间弹窗被释放，并返回结构化警告。
- 保持每个弹窗和项遍历都有界。
- 保持分层支持可选：如果弹窗关联失败，所有者控件仍必须返回有用的关闭状态语义。

## 第 3 阶段要求

- 扩展 Provider 元数据，以包含可选的分层运行时标识和 HWND 标识。
- 为语义子项添加 Provider 钩子，以便将托管所有者控件与关联的分层窗口合并。
- 保持 UIA 作为操作层，RuntimeBridge 作为只读理解层。
