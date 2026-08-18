# AntdUI LayeredWindow Analysis

Stage: Stage 2 source reconnaissance.

This document records how AntdUI popups, dropdowns, overlays, and transient UI surfaces affect RuntimeBridge and HWND inspection. It is documentation only.

## Source Evidence

Layered and popup behavior was found in:

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

## Main Layered Types

| Feature | Runtime Type | Base or Interface | Owner Pattern | Notes |
|---|---|---|---|---|
| Select dropdown | LayeredFormSelectDown | ILayeredShadowForm, SubLayeredForm | Select/Input owner | Separate popup form for dropdown content |
| Select multiple | LayeredFormSelectMultiple and LayeredFormSelectMultipleCheck | internal select layered forms | SelectMultiple owner | Item model with checkbox state |
| Menu dropdown | LayeredFormMenuDown | ILayeredShadowForm, SubLayeredForm | Menu owner | Separate menu popup |
| Context menu | LayeredFormContextMenuStrip | ILayeredShadowFormOpacity, SubLayeredForm | Control/context owner | Not a child control of owner |
| Tooltip | TooltipForm | ILayeredFormOpacity, ITooltip | target control or rectangle | Coordinates from target rect and screen |
| Modal | LayeredFormModal | Window, IEventListener, LayeredFormAsynLoad | config target/form | Dialog or modeless overlay |
| Drawer | LayeredFormDrawer | ILayeredForm, LayeredFormAsynLoad | config form/content | Hosts content and mask handling |
| Message | MessageFrm | ILayeredFormAnimate | global/message config | transient animated surface |
| Notification | NotificationFrm | ILayeredFormAnimate | global/notification config | transient notification surface |

## Runtime Behavior

Layered windows are not reliable descendants of the source control Control.Controls collection. They are separate forms or form-like windows with their own HWND and rendering lifecycle.

Observed behavior:

- Select implements SubLayeredForm, so dropdown UI is associated with but separate from the Select control.
- Menu implements SubLayeredForm and uses menu dropdown layered forms.
- TooltipForm computes placement from a target control or target rectangle.
- LayeredFormDrawer can temporarily host user content, subscribe to owner form movement and size changes, and restore or dispose content on close.
- LayeredFormModal derives from Window and can own nested controls.
- Shadow forms add shadow padding around content. HWND outer bounds and content bounds can differ.
- Layered forms maintain their own Dpi and often derive it from an owner control/form.

## HWND And Managed Tree Impact

RuntimeBridge managed control tree will usually see the owner control but may not see an active popup if the popup is a separate form. Therefore:

- Managed tree should keep the owner control as the stable identity.
- HWND tree should enumerate popups, dialogs, drawers, tooltips, messages, and notifications.
- Provider correlation should link popup HWND/form to owner managed identity when possible.
- Semantic tree may include a synthetic child such as dropdown(open) only when a correlated layered form exists.

Recommended correlation methods:

1. Same process ID.
2. Owner or parent HWND relationship when present.
3. Target rectangle overlap or adjacency to owner control screen bounds.
4. Runtime type name, for example LayeredFormSelectDown.
5. AntdUI SubLayeredForm relationship when available from runtime objects.

## Suggested Snapshot

Suggested popup snapshot fields:

- semanticType: popup.
- provider: AntdUI.
- runtimeType.
- ownerControlId.
- hwnd.
- bounds.
- contentBounds.
- visible.
- dpi.
- item count and bounded items.
- truncated.
- warnings.

For shadow forms, expose both outer HWND bounds and content bounds when available. This avoids false layout diagnostics caused by shadow padding.

## Feature Notes

### Select

Closed state should come from the Select control. Open state should come from LayeredFormSelectDown or related select layered forms. Item traversal must be bounded.

### Menu

Menu data comes from Menu and MenuItemCollection. Active dropdown surfaces should be discovered as LayeredFormMenuDown. Divider and custom button items should become semantic nodes.

### Tooltip

Tooltip location is target-rectangle driven. Represent it as transient popup metadata, not a normal child of the owner control.

### Modal

LayeredFormModal derives from Window, so existing HWND tree should identify it as a real window. Content may be text, control content, or custom content. RuntimeBridge must not execute callbacks.

### Drawer

Drawer can host a Control as content and may temporarily move it or wrap it. Inspection must be read-only and must not force drawer open or closed.

### Message And Notification

MessageFrm and NotificationFrm are transient animated layered forms. Detect them through HWND tree plus runtime type correlation when possible.

## Guardrails

- Do not call show, close, toggle, focus, or activation methods from RuntimeBridge inspection.
- Do not mutate owner form activation, topmost, DPI, theme, content, popup state, selected values, filters, or table state.
- Tolerate popup disposal during inspection and return structured warnings.
- Keep every popup and item traversal bounded.
- Keep layered support optional: if popup correlation fails, owner controls must still return useful closed-state semantics.

## Stage 3 Requirements

- Extend provider metadata to include optional layered runtime identity and HWND identity.
- Add provider hooks for semantic children that can merge managed owner controls with correlated layered windows.
- Preserve UIA as action layer and RuntimeBridge as read-only understanding layer.
