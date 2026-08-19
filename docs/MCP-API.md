# WinForms MCP API (v1 freeze)

This document freezes the MCP tool names, required inputs, output envelopes,
and compatibility rules for the `v1.0.0-rc1` validation line. The source of
truth for discovery is `ToolDefinitionCatalog` and the source of truth for
execution is the corresponding handler. There are **46 tools**. Tool names are
the full `winforms_*` strings shown below; the short names in examples are not
accepted aliases.

## Common protocol and result rules

- The server uses the official Model Context Protocol .NET SDK over stdio.
- Every successful structured result contains `success: true` plus the payload
  listed for that tool.
- A failed call is an MCP error result with structured content:

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

- The registry applies the configured `TOOL_TIMEOUT_MS` (default 30,000 ms),
  links the caller `CancellationToken`, logs elapsed time, and returns `timeout`
  or `cancelled` instead of leaking an exception.
- `imageBase64` is converted to MCP image content on success; the structured
  result omits the base64 field. Other payloads are returned as JSON text and
  structured content.
- Optional fields omitted from a request use the handler defaults. Unknown
  fields are rejected by the published input schema.
- RuntimeBridge is optional. Runtime tools return a structured bridge error when
  the target does not expose a bridge; all UIA tools continue to work without
  it.
- Runtime identities are scoped by `pid` and `bridgeInstanceId`. Runtime and
  diagnostics tools accept optional `bridgeInstanceId`; pass the value from
  `winforms_runtime_status` or a managed snapshot to reject stale references.

Notation in the tables: `required` lists mandatory properties; `optional`
lists accepted properties and their JSON types. `object` and `array` payloads
refer to the DTOs returned by the current implementation, not live WinForms
objects.

## Process

| Tool | Description | Input schema | Output schema | Example | Compatibility notes |
| --- | --- | --- | --- | --- | --- |
| `winforms_launch_app` | Launch an application and return its process ID. | required: `path:string`; optional: `arguments:string`, `workingDirectory:string` | `{success,pid,processName}` | `{"path":"C:/apps/App.exe"}` | Caches the process in the current server session. |
| `winforms_attach_to_process` | Attach by PID or process name. | required: neither; optional: `pid:integer`, `processName:string` (one is required at runtime) | `{success,pid,processName}` | `{"pid":1234}` | Attach is local-machine/UI-session scoped. |
| `winforms_close_app` | Close gracefully or forcefully. | required: `pid:integer`; optional: `force:boolean` | `{success,message}` | `{"pid":1234,"force":false}` | `force:true` terminates; use only for the validation process. |
| `winforms_get_process_status` | Read process state, exit code, responsiveness, title, and stderr. | required: `pid:integer` | `{success,...status fields}` | `{"pid":1234}` | Status fields are sourced from the process service and may be null after exit. |

## UI Automation discovery and inspection

| Tool | Description | Input schema | Output schema | Example | Compatibility notes |
| --- | --- | --- | --- | --- | --- |
| `winforms_find_element` | Find one UIA element and cache it. | optional: `automationId:string`, `name:string`, `className:string`, `controlType:string`, `parent:string` | `{success,elementId,name,automationId,controlType}` | `{"name":"MainForm"}` | Returns a session-local UIA ID; refresh after process restart. |
| `winforms_find_elements` | Find all matching UIA elements and cache them. | same filters as `find_element` | `{success,count,elements:[{elementId,name,automationId,controlType}]}` | `{"controlType":"Button"}` | Results are not managed-control IDs. |
| `winforms_element_exists` | Check an AutomationId. | required: `automationId:string` | `{success,exists:boolean}` | `{"automationId":"btnSave"}` | Uses the bounded UIA worker when enabled, with in-process fallback. |
| `winforms_wait_for_element` | Wait for an AutomationId to appear. | required: `automationId:string`; optional: `timeoutMs:integer` | `{success,exists:boolean,elapsedMs}` | `{"automationId":"grid","timeoutMs":5000}` | Bounded by both `timeoutMs` and the server tool timeout. |
| `winforms_get_element_tree` | Return a bounded UIA tree and cache returned elements. | optional: `pid:integer`, `elementId:string`, `depth:integer`, `maxElements:integer` | `{success,tree,elementCount}` | `{"pid":1234,"depth":3,"maxElements":100}` | This is the UIA tree; use `get_control_tree` for managed Controls. |
| `winforms_get_property` | Read a UIA property or pattern value. | required: `elementId:string`, `propertyName:string` | `{success,propertyName,value}` | `{"elementId":"elem_1","propertyName":"Name"}` | Reads UIA, not managed `TypeDescriptor` properties. |
| `winforms_get_table_data` | Read paged data from a UIA grid/table. | required: `elementId:string`; optional: `startRow:integer`, `rowCount:integer`, `columns:integer[]` | Service result plus `success` (typically `rows`, `columns`, counts) | `{"elementId":"elem_2","rowCount":20}` | Provider support varies; use AntdUI semantic inspection for managed tables. |
| `winforms_get_focused_element` | Return and cache the focused UIA element. | optional: `pid:integer` | `{success,elementId,name,automationId,controlType}` | `{"pid":1234}` | Requires a desktop/UIA focus context. |
| `winforms_list_windows` | List top-level and owned UIA windows. | required: `pid:integer` | `{success,windowCount,windows:[...windowIndex]}` | `{"pid":1234}` | This is UIA/window-service data; `get_window_tree` adds managed/HWND metadata. |
| `winforms_listen_for_event` | Wait for one UIA event. | optional: `elementId:string`; required: `eventType:string`; optional: `timeoutMs:integer` | `{success,fired,eventDetails,elapsedMs}` | `{"eventType":"WindowOpened","timeoutMs":3000}` | Event names are UIA event names, not RuntimeBridge event trace names. |
| `winforms_read_tooltip` | Read tooltip text for a cached element. | required: `elementId:string` | `{success,tooltip}` | `{"elementId":"elem_3"}` | Tooltip availability depends on the target control and desktop state. |
| `winforms_wait_for_condition` | Poll a UIA property comparison. | required: `elementId:string`, `propertyName:string`, `expectedValue:string`; optional: `comparison:string`, `timeoutMs:integer` | `{success,matched,actualValue,elapsedMs}` | `{"elementId":"elem_1","propertyName":"Name","expectedValue":"Ready"}` | Comparisons: `equals`, `contains`, `not_equals`, `greater_than`, `less_than`. |

## UI Automation actions

| Tool | Description | Input schema | Output schema | Example | Compatibility notes |
| --- | --- | --- | --- | --- | --- |
| `winforms_click_element` | Click a cached UIA element. | required: `elementId:string`; optional: `doubleClick:boolean` | `{success,message}` | `{"elementId":"elem_1"}` | Pattern-based where possible; visible desktop may be required by fallback input. |
| `winforms_type_text` | Type text through keyboard simulation. | required: `elementId:string`, `text:string`; optional: `clearFirst:boolean` | `{success,message}` | `{"elementId":"elem_2","text":"NGUS2","clearFirst":true}` | Requires visible desktop/input simulation. |
| `winforms_set_value` | Set a UIA ValuePattern value. | required: `elementId:string`, `value:string` | `{success,message}` | `{"elementId":"elem_2","value":"NGUS2"}` | Works on hidden desktops when the provider exposes ValuePattern. |
| `winforms_drag_drop` | Drag one cached element to another. | required: `sourceElementId:string`, `targetElementId:string` | `{success,message}` | `{"sourceElementId":"elem_1","targetElementId":"elem_2"}` | Visible desktop only. |
| `winforms_send_keys` | Send `SendKeys` syntax. | required: `keys:string`; optional: `pid:integer` | `{success,message}` | `{"keys":"^S","pid":1234}` | Focus/input side effects are desktop-scoped. |
| `winforms_select_item` | Select by text or zero-based index. | required: `elementId:string`; optional: `value:string`, `index:integer` | `{success,message}` or provider result | `{"elementId":"elem_4","value":"Online"}` | Requires a supported selection pattern/provider. |
| `winforms_click_menu_item` | Navigate and click a menu path. | required: `menuPath:string[]`; optional: `pid:integer` | `{success,message}` | `{"menuPath":["File","Open"]}` | Menu labels are matched in order. |
| `winforms_toggle_element` | Toggle checkbox/radio/toggle state. | required: `elementId:string`; optional: `desiredState:string` | `{success,message}` or state result | `{"elementId":"elem_5","desiredState":"on"}` | `desiredState`: `on`, `off`, or `indeterminate`. |
| `winforms_scroll_element` | Scroll using UIA ScrollPattern. | required: `elementId:string`, `direction:string`; optional: `amount:integer`, `scrollType:string` | `{success,message}` or scroll result | `{"elementId":"elem_6","direction":"down","amount":3}` | `direction`: `up`, `down`, `left`, `right`; `scrollType`: `line` or `page`. |
| `winforms_set_table_cell` | Set a UIA grid cell value. | required: `elementId:string`, `row:integer`, `column:integer`, `value:string` | Service result plus `success` | `{"elementId":"elem_7","row":0,"column":2,"value":"Online"}` | Grid/provider support varies. |
| `winforms_manage_window` | Minimize/maximize/restore/move/resize/show/hide/focus a window. | required: `pid:integer`, `action:string`; optional: `width:integer`, `height:integer`, `x:integer`, `y:integer` | Service result plus `success` | `{"pid":1234,"action":"restore"}` | Coordinates and visibility are Windows desktop operations. |
| `winforms_raise_event` | Invoke a supported action on a cached element. | required: `elementId:string`, `eventName:string` | `{success,message}` or action result | `{"elementId":"elem_1","eventName":"Invoke"}` | Only service-approved actions; not arbitrary method invocation. |
| `winforms_open_context_menu` | Open a context menu and cache its root. | required: `elementId:string` | `{success,menuElementId}` | `{"elementId":"elem_1"}` | Popup lifetime is transient; refresh after it closes. |

## Rendering

| Tool | Description | Input schema | Output schema | Example | Compatibility notes |
| --- | --- | --- | --- | --- | --- |
| `winforms_render_form` | Render a Designer file without building the target project. | required: `designerFilePath:string`; optional: `outputPath:string`, `theme:string`, `dpi:integer`, `providerProfile:string` | `{success,imageBase64,renderProfile:{theme,dpi,providerProfile,...}}` plus MCP image content | `{"designerFilePath":"C:/src/MainForm.Designer.cs","theme":"dark","dpi":120}` | RendererHost maps .NET Framework to `net48`, .NET Core 3.x to `netcoreapp3.1`, and .NET 5+ to `net8.0-windows`. |
| `winforms_take_screenshot` | Capture a process window or cached element. | optional: `pid:integer`, `elementId:string`, `outputPath:string` | `{success,imageBase64}` plus MCP image content | `{"pid":1234,"outputPath":"C:/temp/ngus.png"}` | Exactly one target is selected by the service; image data is omitted from structured content after MCP conversion. |

## Runtime inspection

Runtime tools require a target process that references and starts
`Rhombus.WinFormsMcp.RuntimeBridge`. All reads are marshalled to the WinForms
UI thread and only snapshot DTOs cross the named pipe.

| Tool | Description | Input schema | Output schema | Example | Compatibility notes |
| --- | --- | --- | --- | --- | --- |
| `winforms_runtime_status` | Check bridge availability and capabilities. | required: `pid:integer` | `{success,status:{available,connected,protocolVersion,process,capabilities,bridgeInstanceId,error}}` | `{"pid":1234}` | Safe first call; absence of a bridge is a normal result state. |
| `winforms_get_control_tree` | Return bounded managed `Control.Controls` tree. | required: `pid:integer`; optional: `rootId:string`, `maxDepth:integer`, `maxNodes:integer`, `bridgeInstanceId:string` | `{success,roots:[ControlTreeNode],nodeCount,truncated,maxDepth,maxNodes}` | `{"pid":1234,"maxDepth":5,"maxNodes":300}` | Managed IDs are process/bridge scoped; tree summary is intentionally smaller than inspection. |
| `winforms_inspect_control` | Read identity, state, safe properties, layout, bindings, provider, semantic data, and UIA correlation. | required: `pid:integer`, `controlId:string`; optional: `sections:string[]`, `includeProperties:string[]`, semantic paging (`maxDepth`,`maxNodes`,`start`,`count`,`startRow`,`rowCount`,`rowScope`), `bridgeInstanceId:string` | `{success,identity,summary,state,properties,layout,bindings,correlation[,provider][,semantic]}` | `{"pid":1234,"controlId":"ctrl_18","sections":["identity","layout","semantic"]}` | Property getters are allowlisted by default; individual getter failures appear in `properties.errors`. |
| `winforms_get_ancestors` | Return nearest managed parents first. | required: `pid:integer`, `controlId:string`; optional: `bridgeInstanceId:string` | `{success,controlId,ancestors:[ControlAncestorSnapshot]}` | `{"pid":1234,"controlId":"ctrl_18"}` | Ancestor IDs become stale after bridge restart; use the instance ID. |
| `winforms_get_window_tree` | Return bounded HWND/owned/popup tree and provider popup metadata. | required: `pid:integer`; optional: `maxNodes:integer`, `maxItems:integer`, `bridgeInstanceId:string` | `{success,windows:[WindowSnapshot],windowCount}` | `{"pid":1234,"maxNodes":200,"maxItems":100}` | AntdUI layered windows are transient and may disappear during enumeration; warnings are returned in metadata. |
| `winforms_get_bindings` | Read `Control.DataBindings`. | required: `pid:integer`, `controlId:string`; optional: `bridgeInstanceId:string` | `{success,controlId,bindings:[ControlBindingSnapshot],bindingCount}` | `{"pid":1234,"controlId":"ctrl_18"}` | Read-only; no update, replacement, or forced synchronization is performed. |
| `winforms_get_source_mapping` | Map a managed control to Designer locations and event-handler symbols. | required: `pid:integer`, `controlId:string`; optional: `sourceRoot:string`, `maxFiles:integer`, `bridgeInstanceId:string` | `{success,mapping:{control,declaration,initialization,designer,namespace,type,fullyQualifiedType,codeBehindFile,events,source,index,warnings}}` | `{"pid":1234,"controlId":"ctrl_18","sourceRoot":"D:/src/NGUS2"}` | Uses bounded Roslyn indexing. Absolute file paths remain canonical; relative metadata is for VS MCP/CodeGraph handoff. |

## Diagnostics

| Tool | Description | Input schema | Output schema | Example | Compatibility notes |
| --- | --- | --- | --- | --- | --- |
| `winforms_get_clipboard` | Read Windows clipboard text. | none | `{success,text}` | `{}` | Requires an accessible clipboard/UI session. |
| `winforms_set_clipboard` | Write Windows clipboard text. | required: `text:string` | `{success,message}` | `{"text":"NGUS2"}` | Mutates the current user's clipboard; use only in an isolated validation step. |
| `winforms_detect_layout_issues` | Detect bounded layout, DPI, and binding issues. | required: `pid:integer`; optional: `rootId:string`, `checks:string[]`, `maxDepth:integer`, `maxNodes:integer`, `maxDiagnostics:integer`, `bridgeInstanceId:string` | `{success,diagnostics:{processId,bridgeInstanceId,diagnostics,checks,scannedNodes,maxNodes,maxDiagnostics,truncated}}` | `{"pid":1234,"checks":["layout","dpi"],"maxNodes":300}` | Evidence-based diagnostics only; this API does not auto-fix or edit Designer code. |
| `winforms_compare_screenshot` | Deterministic bounded PNG pixel diff. | optional: exactly one of `beforePath:string`/`beforeBase64:string` and exactly one of `afterPath:string`/`afterBase64:string`; optional: `maxRegions:integer`, `pixelThreshold:integer` | `{success,diff:{...pixel counts/regions/bounds...}}` | `{"beforePath":"C:/temp/before.png","afterPath":"C:/temp/after.png"}` | Does not infer control semantics; pair with runtime/source inspection for diagnosis. |
| `winforms_check_accessibility` | Check managed accessibility metadata and UIA patterns. | required: `pid:integer`; optional: `rootId:string`, `maxDepth:integer`, `maxNodes:integer`, `maxDiagnostics:integer`, `bridgeInstanceId:string` | `{success,accessibility:{processId,controls,diagnostics,scannedNodes,maxNodes,maxDiagnostics,truncated}}` | `{"pid":1234,"maxNodes":200}` | UIA correlation is best-effort and includes method/confidence when available. |
| `winforms_start_event_trace` | Start a bounded read-only RuntimeBridge event trace. | required: `pid:integer`; optional: `rootId:string`, `events:string[]`, `maxEvents:integer`, `durationMs:integer`, `maxNodes:integer`, `bridgeInstanceId:string` | `{success,trace:{traceId,processId,bridgeInstanceId,active,startedAtUtc,expiresAtUtc,maxEvents,subscribedControlCount,subscribedEvents,nextSequence,...}}` | `{"pid":1234,"events":["Click","TextChanged"],"durationMs":10000}` | Only the documented WinForms event whitelist is subscribed. |
| `winforms_read_event_trace` | Read new events from a trace session. | required: `pid:integer`, `traceId:string`; optional: `afterSequence:integer`, `maxEvents:integer`, `bridgeInstanceId:string` | `{success,trace:{events,nextSequence,droppedEventCount,truncated,...}}` | `{"pid":1234,"traceId":"trace_1","afterSequence":0}` | Use returned `nextSequence` as the next cursor; traces expire and are bounded ring buffers. |
| `winforms_stop_event_trace` | Stop a trace and detach handlers. | required: `pid:integer`, `traceId:string`; optional: `bridgeInstanceId:string` | `{success,trace:{active:false,...}}` | `{"pid":1234,"traceId":"trace_1"}` | Safe to call during cleanup; stale instance IDs are rejected. |

## RuntimeBridge and source compatibility

The optional bridge package is intentionally framework-neutral at the contract
boundary:

- `Rhombus.WinFormsMcp.RuntimeContracts`: `netstandard2.0` DTOs and Protocol v1.
- `Rhombus.WinFormsMcp.RuntimeBridge`: `net48` and `net8.0-windows`.
- The bridge must be started from the target application's UI thread and should
  be stopped during form shutdown. It returns snapshots only.
- A `net472` application such as NGUS2 cannot reference the current `net48`
  bridge without a compatibility experiment. UIA automation and rendering do
  not require a bridge and remain the fallback path.
- No tool in this document authorizes editing Designer code, invoking VS MCP,
  invoking CodeGraph MCP, arbitrary reflection, or changing runtime properties.

## Frozen compatibility policy

For v1.x, changing a tool name, required input, output meaning, or the semantic
interpretation of an existing field is a breaking change. Additive optional
fields are allowed only when the old response remains valid. New tools require
evidence from a real workflow and a corresponding API review; they are not part
of the RC validation by default.
