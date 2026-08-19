# CodeGraph MCP Contract Analysis

## Scope

This document records the observable MCP contract of the local CodeGraph
reference repository at commit `c6aaa20`. It is an integration analysis only.
WinForms MCP does not add a CodeGraph client, transport, package reference, or
cross-process call.

CodeGraph tools are read-only queries over a pre-built local graph. They can
query the default project or another indexed project through `projectPath`.

## Relevant Tool Contracts

| Tool | Required handoff | Useful optional handoff | Purpose |
| --- | --- | --- | --- |
| `codegraph_search` | `query` | `kind`, `limit`, `projectPath` | Locate symbols by name or partial name |
| `codegraph_callers` | `symbol` | `file`, `limit`, `projectPath` | Discover callers |
| `codegraph_callees` | `symbol` | `file`, `limit`, `projectPath` | Discover downstream calls |
| `codegraph_impact` | `symbol` | `file`, `depth`, `projectPath` | Bound impact analysis |
| `codegraph_node` | none | `symbol`, `file`, `line`, `includeCode`, `projectPath` | Resolve a precise node or source context |
| `codegraph_explore` | `query` | `maxFiles`, `projectPath` | Broader source and graph exploration |
| `codegraph_status` / `codegraph_files` | none | `projectPath` | Check index state and indexed files |

All query tools accept a `projectPath` that may be the project root or a
directory below it. CodeGraph resolves the nearest `.codegraph` index above that
path. This lets a client target an indexed subproject without coupling servers.

## Symbol and File Resolution

CodeGraph resolves names fuzzily, including qualified suffixes. An FQN is a
high-quality discovery hint but not a compiler-backed globally unique key. The
`file` parameter disambiguates same-named members, and `line` further anchors
`codegraph_node` to a concrete declaration.

Internally CodeGraph stores project-relative forward-slash paths. Its tool
contract accepts a path or suffix, so WinForms MCP exposes both:

- Existing absolute `file` values remain authoritative for editor/debugger use.
- Optional `projectRelativeFile` is convenient for a CodeGraph `file` hint.
- Optional `sourceRoot` is the preferred initial `projectPath` hint. CodeGraph
  can walk upward to the actual `.codegraph` root.

## Recommended Handoff

Use the complete tuple rather than an FQN alone:

```json
{
  "symbol": "NGUS2.UI.Forms.DeviceManagementForm.BtnUpgrade_Click",
  "file": "Forms/DeviceManagementForm.cs",
  "line": 823,
  "projectPath": "C:/repo"
}
```

The robust sequence is:

1. Use `codegraph_node(symbol, file, line, projectPath)` to verify resolution.
2. Use `codegraph_callers`, `codegraph_callees`, or `codegraph_impact` with the
   same symbol, file, and project context.
3. Return to VS MCP for editing/build/debug and WinForms MCP for validation.

An unindexed path is not a WinForms mapping failure. CodeGraph can report how to
pass a valid `projectPath` or initialize an index.

## Boundaries

WinForms MCP emits metadata only. It never triggers CodeGraph indexing, sync, or
mutation, and does not serialize CodeGraph's internal node IDs into its protocol.
