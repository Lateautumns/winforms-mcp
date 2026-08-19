# Cross-MCP Metadata Schema

## Purpose

This schema makes a WinForms runtime control useful to separate source, graph,
and IDE MCP servers without giving WinForms MCP a dependency on them. Metadata
is additive to `winforms_get_source_mapping`; existing fields and required
parameters remain unchanged.

```text
Managed control snapshot
  -> WinForms source mapping
  -> source identity and exact locations
  -> CodeGraph analysis or VS navigation/debugging
  -> WinForms runtime validation
```

RuntimeBridge remains read-only. No MCP server invokes another MCP server.

## SourceIdentitySnapshot

`SourceIdentitySnapshot` is optional. It appears as `mapping.source` for the
owning Form type and `mapping.events[event].source` for an event handler. It is
a handoff record, not a compiler symbol key.

| Field | Meaning | Consumer guidance |
| --- | --- | --- |
| `project` | Discovered `.csproj` file stem | Human-readable project label |
| `projectPath` | Absolute discovered `.csproj` path | VS MCP `build_project` input |
| `sourceRoot` | Canonical bounded scan root | Preferred CodeGraph `projectPath` hint |
| `file` | Absolute source file | Canonical VS MCP document/debugger path |
| `projectRelativeFile` | Forward-slash path relative to `sourceRoot` | CodeGraph `file` hint |
| `line`, `column`, `endLine`, `endColumn` | 1-based source span | VS navigation; CodeGraph disambiguation |
| `namespace`, `type`, `fullyQualifiedType` | Owner type metadata | Search and display context |
| `member`, `memberKind`, `method` | Type/member identity | Simple-name fallback search |
| `fullyQualifiedSymbol` | Readable qualified symbol hint | CodeGraph discovery, not a canonical key |
| `runtimeControlId`, `runtimeControlName`, `runtimeControlType` | Runtime-to-source link | Return to managed/UIA inspection |

Nullable fields are omitted when unknown. Consumers must not infer a missing
project, source root, or symbol from the absence of one optional field.

## Existing Source Mapping Fields

The existing top-level properties remain the compatibility layer:

- `control` is the managed runtime identity.
- `declaration`, `initialization`, and `designer` retain absolute `file` and
  1-based spans. They may add optional `projectRelativeFile`.
- `namespace`, `type`, `fullyQualifiedType`, and `codeBehindFile` retain their
  original semantics.
- `events[event]` retains `event`, `method`, `file`, `line`, and
  `fullyQualifiedSymbol`. It may add precise `location` and `source` objects.

Older clients can ignore all additions and still navigate from original fields.

## Example

```json
{
  "control": {
    "managedId": "ctrl_18",
    "name": "btnUpgrade",
    "type": "AntdUI.Button"
  },
  "fullyQualifiedType": "NGUS2.UI.Forms.DeviceManagementForm",
  "source": {
    "project": "NGUS2.UI",
    "projectPath": "C:/repo/NGUS2.UI/NGUS2.UI.csproj",
    "sourceRoot": "C:/repo",
    "file": "C:/repo/NGUS2.UI/Forms/DeviceManagementForm.cs",
    "projectRelativeFile": "NGUS2.UI/Forms/DeviceManagementForm.cs",
    "line": 12,
    "column": 15,
    "namespace": "NGUS2.UI.Forms",
    "type": "DeviceManagementForm",
    "fullyQualifiedType": "NGUS2.UI.Forms.DeviceManagementForm",
    "member": "DeviceManagementForm",
    "memberKind": "type",
    "fullyQualifiedSymbol": "NGUS2.UI.Forms.DeviceManagementForm",
    "runtimeControlId": "ctrl_18",
    "runtimeControlName": "btnUpgrade",
    "runtimeControlType": "AntdUI.Button"
  },
  "events": {
    "Click": {
      "method": "BtnUpgrade_Click",
      "file": "C:/repo/NGUS2.UI/Forms/DeviceManagementForm.cs",
      "line": 823,
      "fullyQualifiedSymbol": "NGUS2.UI.Forms.DeviceManagementForm.BtnUpgrade_Click",
      "location": {
        "file": "C:/repo/NGUS2.UI/Forms/DeviceManagementForm.cs",
        "projectRelativeFile": "NGUS2.UI/Forms/DeviceManagementForm.cs",
        "line": 823,
        "column": 18,
        "endLine": 823,
        "endColumn": 66
      }
    }
  }
}
```

## Consumer Rules

1. Prefer absolute `file` plus 1-based `line` and `column` for VS MCP.
2. Prefer `fullyQualifiedSymbol`, `projectRelativeFile`, `line`, and
   `sourceRoot` together for CodeGraph. Use `file` to disambiguate names.
3. Treat `fullyQualifiedSymbol` as a hint. Overloads, generated code, partial
   types, and third-party indexes may need location fields for unique resolution.
4. Preserve source coordinate bases. CodeGraph internal columns differ; use the
   line as a disambiguator and let CodeGraph resolve the node.
5. Never expose CodeGraph node IDs, VS automation objects, live `Control`
   instances, or arbitrary reflection values through this contract.
