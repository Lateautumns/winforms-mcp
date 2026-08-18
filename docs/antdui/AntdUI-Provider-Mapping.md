# AntdUI Provider Mapping

Stage: Stage 2 source reconnaissance.

This document maps the local AntdUI source model into the future provider design. It is documentation only and does not implement AntdUIProvider.

## Detection Strategy

RuntimeBridge core should not reference AntdUI at compile time. AntdUI detection can be done by reflection:

- Runtime type namespace starts with AntdUI.
- Base type chain contains AntdUI.IControl.
- Popup or transient window base/interface names include ILayeredForm, ILayeredShadowForm, ILayeredFormOpacity, ILayeredFormAnimate, or known LayeredForm* runtime names.

The provider should run all control reads on the WinForms UI thread and return DTO snapshots only.

## Control Mapping

| Control | Runtime Type | Semantic Type | Public Properties | Reflection Members | Semantic Children | Special State | Layered | Risk | Priority |
|---|---|---|---|---|---|---|---:|---|---:|
| Button | AntdUI.Button | button | Text, Icon, IconSvg, Loading, LoadingSvg, AutoToggle, Toggle, DialogResult, ColorScheme, Dpi | Avoid render internals | none | loading, toggle, hover, active | No | Low | 1 |
| Input | AntdUI.Input | textbox | Text, PlaceholderText/localization, prefix/suffix text/svg, MaxLength, ReadOnly, Multiline, AcceptsTab, UseContextMenu, VirtualMode, ColorScheme, Dpi | Text style collection only when requested | optional logical text lines | caret, selection, scroll | Context menu/history possible | Medium | 1 |
| InputNumber | AntdUI.InputNumber | number-input | Input properties, Value, Minimum, Maximum, Increment, AlwaysShowControl, InterceptArrowKeys | Avoid spin button layout internals | spin buttons as semantic actions | numeric value | Inherits Input behavior | Medium | 1 |
| Checkbox | AntdUI.Checkbox | checkbox | Text, Checked, AutoCheck, ColorScheme, Dpi | none by default | none | checked | No | Low | 1 |
| Radio | AntdUI.Radio | radio | Text, Checked, AutoCheck, ColorScheme, Dpi | none by default | parent radio group from managed scan | checked | No | Low | 1 |
| Switch | AntdUI.Switch | switch | Checked, Text, ColorScheme, Dpi | none by default | none | checked/loading | No | Low | 1 |
| Select | AntdUI.Select | select | Items, SelectedIndex, SelectedItem, SelectedValue, Text, ColorScheme, Dpi | popup form reference only if public path is insufficient | select items; dropdown items when open | selection, open popup | Yes | Medium | 1 |
| Tabs | AntdUI.Tabs | tabs | Items/pages, SelectedIndex, SelectedPage, Text, Enabled, Visible | avoid designer services at runtime | tab pages | selected page | No | Medium | 1 |
| Tree | AntdUI.Tree | tree | Items, TreeItem ID/Name/Text/Tag, Checked, Expanded, Selected, Depth | layout/scroll cache only when bounds requested | tree nodes | checked, expanded, selected | No | Medium | 1 |
| Table | AntdUI.Table | table | Columns, DataSource, VirtualMode, MultipleRows, filter/sort config, selection | dataTmp, rows, RowsCache, rowsFilter, SortHeader; bounded only | header, rows, cells, cell buttons | sort, filter, selection, virtualization | Filter popups | High | 1 |
| Menu | AntdUI.Menu | menu | Items, MenuItem text/icon/enabled/visible | layered dropdown state only when needed | menu items, buttons, dividers | selected, expanded | Yes | Medium | 2 |

## Default Property Policy

The first provider should use a strict allowlist:

- Common WinForms properties already captured by RuntimeBridge: Name, Text, Bounds, ClientSize, Visible, Enabled, Handle, Dock, Anchor, Padding, Margin, Font, ForeColor, BackColor.
- Common AntdUI: ColorScheme, Dpi, HandCursor.
- Button: Icon, IconSvg, Loading, LoadingSvg, LoadingRespondClick, AutoToggle, Toggle, ToggleText, DialogResult.
- Input: PlaceholderText, PrefixText, PrefixSvg, SuffixText, SuffixSvg, MaxLength, ReadOnly, Multiline, AcceptsTab, AcceptsEscape, HideSelection, UseContextMenu, VirtualMode.
- InputNumber: Value, Minimum, Maximum, Increment, AlwaysShowControl, InterceptArrowKeys.
- Checkbox/Radio/Switch: Checked, AutoCheck.
- Select: Items, SelectedIndex, SelectedItem, SelectedValue.
- Tabs: Items, SelectedIndex, SelectedPage.
- Tree: Items, selected node, checked and expanded node metadata.
- Table: Columns, DataSource type, VirtualMode, MultipleRows, FilterRealTime.

Every getter should be isolated. A failing getter returns a per-property error instead of failing inspect_control.

## Semantic Children

| Semantic Type | Children | Bounds | Paging |
|---|---|---:|---:|
| tabs | tab pages | rendered tab header bounds when available | yes for large page counts |
| tree | tree nodes | node bounds when layout cache is available | yes |
| table | header, visible rows, cells, cell buttons | yes | yes |
| menu | menu items, buttons, dividers | item bounds when visible | yes |
| select | items and open dropdown items | dropdown item bounds when open | yes |

Recommended defaults:

- maxDepth: 4 for tree-like controls.
- maxNodes: 200 for semantic trees.
- startRow and rowCount: 0 and 50 for tables.
- includeOffscreen: false unless explicitly requested.

## Table Provider Plan

The Table provider should be layered:

1. Public summary: type, name, bounds, column count, data source type, virtual mode, selection summary.
2. Column summary: key, title, width, read-only, filter, sort, fixed flags.
3. Visible row summary: bounded row templates if layout cache is available.
4. Cell summary: column key, display value, raw value if safe, semantic cell type.
5. Cell actions: button, checkbox, radio, and switch cells based on table cell classes and render metadata.

If internal reflection fails, return a degraded public summary with warnings.

## Tree Provider Plan

Tree nodes should be returned as bounded semantic nodes with:

- semanticType: tree-node.
- id, name, text, tag if safe.
- depth, childCount.
- checked, expanded, selected.
- bounds when available.
- truncated flag when maxDepth or maxNodes is reached.

Prefer public item collections. Avoid scroll/render caches unless the caller requests bounds.

## Tabs Provider Plan

Tabs should expose tab pages as semantic children:

- semanticType: tab-page.
- text/localization text.
- selected, enabled, visible.
- page index.
- associated controls if they are present in managed children.

Tabs.Design.cs includes ParentControlDesigner, DesignerActionList, IDesignerHost, and selection services. Runtime inspection must not call designer services.

## Select Provider Plan

Closed Select state should come from the Select control itself:

- items.
- selected index/value/item.
- text.
- bounds and enabled/visible state.

Open dropdown state should come from correlated LayeredFormSelectDown or related layered forms. Do not assume dropdown items are in Control.Controls.

## Risks And Guardrails

- AntdUI internal member names are version-sensitive.
- Table internals are expensive and must be bounded.
- Layered windows are separate forms/windows and can disappear during inspection.
- RuntimeBridge remains read-only: no property setters, method invocation, global Config mutation, filter mutation, or selection mutation.
- Reflection access must be centralized and covered by tests in Stage 3.
