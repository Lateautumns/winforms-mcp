# AntdUI Architecture Analysis

Stage: Stage 2 source reconnaissance.

Scope: read-only analysis of the local AntdUI reference source. This document does not implement an AntdUI provider and does not add AntdUI as a compile-time dependency to RuntimeContracts, RuntimeBridge core, or Server core.

Reference source root: `D:\06_开源工具重写\AntdUIAntdUI`

## Source Evidence

The analysis below was derived from these source files:

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

## Inheritance

Most AntdUI controls are real WinForms controls through the AntdUI `IControl` base class:

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

Important special cases:

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

Provider implication: the first AntdUI provider can identify controls by `control.GetType().FullName` and by walking base types for `AntdUI.IControl`. It should not require a direct AntdUI reference in RuntimeBridge core.

## Common IControl Runtime Model

`IControl` is the main shared runtime surface:

- It inherits `System.Windows.Forms.Control`, so it has a normal HWND when created.
- It overrides `Visible` and contains thread-aware set behavior through `InvokeRequired`.
- It exposes `ColorScheme` as `TAMode` with default `Auto`.
- It exposes `Dpi`, which first honors `Config._dpi_custom`, then uses `BaseForm.Dpi`, `ILayeredForm.Dpi`, or screen DPI.
- It uses `Config.TouchEnabled`, `Config.TouchThreshold`, `Config.MouseHoverDelay`, `Config.TouchClickEnabled`, and animation settings.
- It exposes `RenderRegion` and paint/draw events, but these are graphics-heavy and should not be read by default.

Provider implication: `IControl` gives a good generic AntdUI identity, theme, DPI, and shape baseline. The provider should read public scalar and string properties first and avoid graphics objects unless explicitly requested.

## Control Summaries

| Control | Runtime Type | Base Type | Composite | Internal Model | Layered Window | Designer |
|---|---|---|---:|---:|---:|---:|
| Button | `AntdUI.Button` | `IControl` | No | Medium | No | Standard |
| Input | `AntdUI.Input` | `IControl` | No | High | Context menu/history | Standard |
| InputNumber | `AntdUI.InputNumber` | `Input` | No | Medium | Inherits Input behavior | Standard |
| Checkbox | `AntdUI.Checkbox` | `IControl` | No | Low | No | Standard |
| Radio | `AntdUI.Radio` | `IControl` | No | Low | No | Standard |
| Switch | `AntdUI.Switch` | `IControl` | No | Low | No | Standard |
| Select | `AntdUI.Select` | `Input` | No | High | Yes | Standard |
| Tabs | `AntdUI.Tabs` | `IControl` | Logical pages | High | No | Custom designer |
| Tree | `AntdUI.Tree` | `IControl` | Logical nodes | High | No | Content serialization |
| Table | `AntdUI.Table` | `IControl` | Logical rows/cells | Very high | Filter popups | Content and hidden state |
| Menu | `AntdUI.Menu` | `IControl` | Logical menu items | High | Yes | Content serialization |

## Safe Public Property Groups

These properties are high-value and low-risk for the provider because they are public API and mostly scalar or DTO-like:

- Common WinForms: `Name`, `Text`, `Bounds`, `ClientSize`, `Visible`, `Enabled`, `Handle`, `Dock`, `Anchor`, `Padding`, `Margin`, `Font`, `ForeColor`, `BackColor`.
- AntdUI common: `ColorScheme`, `Dpi`, `HandCursor`, `HandDragFolder`.
- Button: hover/active colors, icon fields, loading fields, toggle fields, `DialogResult`.
- Input: prefix/suffix text, placeholder localization text, `MaxLength`, `AcceptsTab`, `AcceptsEscape`, `HideSelection`, `UseContextMenu`, `VirtualMode`, loading fields.
- InputNumber: `Increment`, `AlwaysShowControl`, `InterceptArrowKeys`, `EnabledValueTextChange`, numeric value fields exposed through InputNumber public API.
- Checkbox/Radio/Switch: checked state, `AutoCheck`, localization text, checked-change events.
- Select: `Items`, selected values/indexes, popup/dropdown state exposed through public API.
- Tabs: page collection, selected index/page, page text/enabled/visible.
- Tree: item collection, node text/name/id, checked/expanded/selected state, depth and child collection.
- Table: columns, data source, virtual mode, selection, filters, sort state, rendered row cache and template row/cell structures.

## Reflection Classification

Use reflection only for data that is not available through public API and is necessary for semantic inspection:

| Member Area | Access | Meaning | Risk | Version Sensitivity |
|---|---|---|---|---|
| `IControl.Dpi` | Public | Runtime scale factor | Low | Low |
| `IControl.ColorScheme` | Public | Light/dark/auto intent | Low | Low |
| `Table.rows` / layout cache | Internal field | Rendered rows and visible cells | Medium | High |
| `Table.dataTmp` / rows cache | Internal field | normalized data source rows | Medium | High |
| `Table.SortHeader` | Internal/public depending context | sort state | Medium | Medium |
| `Table.rowsFilter` / filter cache | Internal | active filter result | Medium | High |
| `LayeredForm*` classes | Internal | popup/dialog/dropdown windows | Medium | High |
| Graphics paths/bitmaps/canvases | Internal/public | drawing state | High | High |

Default provider behavior should avoid internal fields unless the requested semantic feature cannot be built from public API. Every reflection read should be best-effort, per-member isolated, and return warnings instead of failing the whole inspection.

## Unsafe Or Avoided Reads

Avoid reading these by default:

- `Graphics`, `Canvas`, `GraphicsPath`, `Bitmap`, `SafeBitmap`, cached shadow images, or any property that creates drawing resources.
- Getter paths that measure text, capture controls, render bitmaps, or call `Print`.
- Native wrappers and Win32 structures except for stable HWND-related metadata already available from WinForms or the Server HWND inspection layer.
- Collections with unbounded row/item counts unless `maxNodes`, `start`, `count`, `rowCount`, or equivalent bounds are supplied.

## Table Model

Table is the most complex first-wave AntdUI control. Evidence from `Table.cs`, `Table.Data.cs`, `Table.Layout.cs`, `Table.Filter.cs`, and `Table.Template.cs` shows:

- `Table : IControl, IEventListener, IScrollBar`.
- Public configuration includes `VirtualMode`, `MultipleRows`, `DefaultExpand`, `FilterRealTime`, `FilterShowCheckBg`, `FilterSortOrder`, `EditAutoHeight`, `EditLostFocus`, and many column/cell behavior settings.
- Data ingestion normalizes `DataSource` into internal row/cache structures in `Table.Data.cs`.
- Filtering uses `RowsCache`, `rowsFilter`, and filter matching in `Table.Filter.cs`.
- Layout builds row templates and visible cells in `Table.Layout.cs`.
- Cell value extraction uses column keys and `PropertyDescriptor.GetValue` paths.
- Virtual mode changes row traversal and visible row materialization. The provider must respect row limits.

Answers for Stage 2:

- Columns come from `Column` definitions in `Table.cs`.
- Data rows come from `DataSource` normalized by `Table.Data.cs`.
- Visible/rendered rows come from `Table.Layout.cs` row templates and row list caches.
- Cell values are resolved by column key and property descriptors or rendered values.
- Cell buttons are represented by table cell button classes and render code in `Table.Render.Button.cs`.
- Selected row state is held in row/template state and selection helpers; first implementation should expose only what can be read safely.
- Sort state is tied to `SortHeader` and layout/sort paths.
- Filter state is tied to `Filter` definitions and `rowsFilter`.
- Virtualization is explicit via `VirtualMode`.

## Tree Model

Tree evidence from `Tree.cs` shows:

- `Tree : IControl, IEventListener, IScrollBar`.
- It owns `TreeItemCollection` and `TreeItem`.
- `TreeItem` exposes ID/name/text/localization fields, tag, depth, child items, and state fields for checked/expanded/selected behavior.
- The provider should expose semantic nodes bounded by `maxDepth` and `maxNodes`.

## Tabs Model

Tabs evidence from `Tabs.cs` and `Tabs.Design.cs` shows:

- `Tabs : IControl, IEventListener`.
- It owns logical `TabPage` items rather than ordinary child controls for every page semantic.
- `Tabs.Design.cs` includes a `ParentControlDesigner`, `DesignerActionList`, `IDesignerHost`, and selection service integration.
- Provider should expose `Tabs` as semantic children: `TabPage` nodes with text, enabled/visible, selected state, and associated controls when present.

## Select Model

Select evidence from `Select.cs` and layered window classes shows:

- `Select : Input, SubLayeredForm`.
- It owns `SelectItem` data and selected item/index/value state.
- Dropdown UI uses layered form classes such as `LayeredFormSelectDown`.
- Dropdown popups are not reliable as `Control.Controls` children of the Select control. They should be discovered through HWND/window inspection and correlated to the owner control.

## Theme And DPI

Evidence from `Config.cs`, `ThemeConfig.cs`, `Helper.DPI.cs`, `BaseForm.cs`, `Window.cs`, and `IControl.cs` shows:

- `TAMode` is used for control color scheme intent such as Auto/light/dark behavior.
- `Config` owns global runtime behavior such as animation, shadow, touch, hover delay, and custom DPI.
- `IControl.Dpi` can derive from `Config._dpi_custom`, `BaseForm.Dpi`, `ILayeredForm.Dpi`, or screen DPI.
- `Window` extends `BaseForm` and participates in custom window behavior.

Provider implication: theme and DPI should be surfaced as runtime snapshots. Do not mutate global `Config` state from inspection tools.

## Designer Compatibility

Designer evidence:

- Many controls use normal WinForms attributes such as `ToolboxItem`.
- `Tabs.Design.cs` contains custom designer support with `ParentControlDesigner`, `DesignerActionList`, and `IDesignerHost`.
- Some collections use `DesignerSerializationVisibility.Content`; some runtime properties are hidden.

Renderer implication: `DesignSurfaceFormRenderer` must be prepared for custom designer metadata and collection serialization, but Stage 2 does not modify renderer behavior.

## Provider Design Notes For Stage 3

- Add a provider registry that can choose `StandardWinFormsProvider` first and `AntdUIProvider` when `FullName` or base type matches `AntdUI.*`.
- Keep AntdUI detection reflection-only in RuntimeBridge core, or place AntdUI-specific logic in a separate provider assembly that does not force AntdUI references into core projects.
- Keep semantic tree calls bounded. Table, Tree, Tabs, Menu, and Select must support pagination or `maxNodes`.
- RuntimeBridge remains read-only. No property setters, arbitrary reflection execution, or method invocation.
- Continue to use UI thread dispatch for all runtime control reads.
