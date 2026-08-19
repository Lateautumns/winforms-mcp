# VS MCP Contract Analysis

## Scope

This document records the observable source-navigation contract of the local
VS-MCPServer reference repository at commit `1d020ae`. It is an integration
analysis only. WinForms MCP does not reference, start, or call VS MCP.

The navigation, document, solution, build, debugger, and shared-model source was
reviewed. This analysis describes the contract that an MCP client can compose,
not an internal VS-MCPServer API.

## Relevant Tool Contracts

| Tool | Required handoff | Result that matters to WinForms workflow |
| --- | --- | --- |
| `symbol_document` | Absolute source `path` | Symbols in one open-solution file |
| `symbol_workspace` | Name or partial-name `query` | Candidate symbols and locations; not a direct FQN lookup |
| `goto_definition` | Absolute `path`, 1-based `line`, 1-based `column` | Definition for the symbol at that position |
| `find_references` | Absolute `path`, 1-based `line`, 1-based `column`, optional maximum | References for the symbol at that position |
| `document_open` / `document_read` | Absolute `path` | Editor navigation and bounded file inspection |
| `selection_set` | Absolute `path`, 1-based start/end positions | Cursor or selected source range |
| `build_project` | Full absolute `.csproj` path | Project build; its parameter name is misleading |
| `debugger_add_breakpoint` | Absolute `path`, line | Breakpoint for an event handler or declaration |

`SymbolInfo` returns `Name`, `FullName`, `Kind`, `FilePath`, 1-based start/end
positions, container name, and children. `LocationInfo` returns an absolute file
location plus preview. Debugger state and breakpoints likewise use file and line
coordinates.

## Coordinate and Path Rules

1. WinForms MCP retains absolute file paths for all existing source locations.
   VS MCP requires them for navigation, documents, builds, and debugging.
2. All WinForms MCP source coordinates are 1-based. This matches VS MCP.
3. A `fullyQualifiedSymbol` helps discovery but cannot invoke
   `goto_definition` or `find_references`; those tools need a source position.
4. An event handler location is normally the best breakpoint target. A control
   declaration or initialization location is better for Designer wiring.
5. `symbol_workspace` is name/partial-name search. Use the simple member or type
   name, then disambiguate with absolute path and source location.

## Recommended Handoff

For a runtime control named `btnUpgrade`, use the event source identity when a
Click handler exists:

```json
{
  "file": "C:/repo/Forms/DeviceManagementForm.cs",
  "line": 823,
  "column": 18,
  "fullyQualifiedSymbol": "NGUS2.UI.Forms.DeviceManagementForm.BtnUpgrade_Click"
}
```

A client can then:

1. Call `document_open(path)` or place the cursor with `selection_set`.
2. Call `goto_definition` for a referenced symbol or `find_references` for the
   handler itself.
3. Call `debugger_add_breakpoint(path, line)` before reproducing the UI action.
4. Use WinForms MCP UIA tools for the action and RuntimeBridge for managed state.

## Boundaries

WinForms MCP does not assume an open Visual Studio solution, modify documents,
or invoke debugger evaluation. It produces read-only bounded metadata. Missing
project discovery omits optional metadata; source locations already found remain
usable.
