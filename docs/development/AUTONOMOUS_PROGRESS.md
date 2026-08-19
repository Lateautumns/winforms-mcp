# Autonomous Progress

## Stage

Stage 6 - AntdUI LayeredWindow / Popup inspection complete; local Gate and PR #2 Windows Core CI passed. Continue to Stage 7.

## Implemented

- Completed PR #1 Stage 0 RuntimeBridge lifecycle hardening on feature/v11-foundation-refactor.
- Confirmed PR #1 Core CI green on commit bd19b0ea49a50441d6dbb8f7c75fba61f3d3f652.
- Created stacked development branch feature/v14-antdui-provider from feature/v11-foundation-refactor.
- Confirmed AntdUI reference repository exists at D:\06_开源工具重写\AntdUIAntdUI.
- Completed Stage 2 AntdUI source reconnaissance documents:
  - docs/antdui/AntdUI-Architecture-Analysis.md.
  - docs/antdui/AntdUI-Provider-Mapping.md.
  - docs/antdui/AntdUI-LayeredWindow-Analysis.md.
- Added RuntimeBridge control provider architecture:
  - IControlProvider.
  - IControlProviderRegistry.
  - ControlProviderRegistry with priority-based resolution.
  - StandardWinFormsProvider fallback.
- Added optional RuntimeContracts provider and semantic snapshots.
- Added reflection-based AntdUIProvider for basic AntdUI controls:
  - Button.
  - Input.
  - InputNumber.
  - Checkbox.
  - Radio.
  - Switch.
  - Select.
- Added Rhombus.WinFormsMcp.AntdUI.TestApp for real RuntimeBridge E2E coverage.
- Added AntdUI provider unit tests and AntdUI RuntimeBridge integration tests.
- Added bounded semantic inspection for AntdUI Tabs, Tree, Table, and Menu through the existing inspect_control protocol.
- Added semantic paging controls for top-level collections and table rows (start/count/startRow/rowCount/rowScope) with truncation metadata.
- Added AntdUI Table columns, data/visible/rendered row scopes, sort/filter metadata, cell values, and CellButton snapshots.
- Added complex semantic tree fixtures and end-to-end coverage to the AntdUI TestApp.
- Added read-only provider window metadata for AntdUI layered forms:
  - Select dropdown.
  - Menu popup.
  - Tooltip.
  - Modal-compatible layered surfaces.
  - Drawer.
  - Message and notification-compatible layered surfaces.
- Added bounded popup item snapshots, selected/highlighted state, visible range,
  content/target bounds, DPI, owner managed identity/path, and per-window warnings.
- Extended the existing `winforms_get_window_tree` request with optional `maxItems`;
  no new MCP tool was added.
- Added real AntdUI layered-window E2E coverage for Select, Menu, Tooltip, Message,
  and Drawer owner correlation and bounded metadata.
- Hardened UIA correlation fallback for managed controls using automation id, native HWND lookup, bounded HWND traversal, and process matching.
- Hardened UI text input fallback for controls without a writable ValuePattern by trying writable child value patterns, STA clipboard paste, and paced SendKeys fallback.

## Architecture

- RuntimeBridge remains read-only inspection infrastructure.
- RuntimeBridge core, RuntimeContracts, and Server core still have no AntdUI compile-time dependency.
- AntdUI compile-time dependency is limited to Rhombus.WinFormsMcp.AntdUI.TestApp.
- AntdUIProvider uses controlled reflection over allowlisted public properties with per-property error isolation.
- Provider matching remains centralized in ControlProviderRegistry.
- StandardWinFormsProvider remains the fallback for common WinForms controls and unknown third-party controls.
- Protocol remains RuntimeBridge Protocol v1; semantic data is added through optional fields.
- Window snapshots preserve all existing fields and add optional `providerWindowMetadata`.
- AntdUI layered-window discovery is based on type identity plus a controlled
  reflection allow-list; it never invokes arbitrary methods or mutates popup state.
- Provider/semantic snapshots are built on the WinForms UI thread through the existing RuntimeBridge inspector path.
- Managed RuntimeBridge remains the understanding layer; UIA remains the action layer.
- Semantic reads remain bounded by RuntimeBridge clamps and provider-level collection/row limits; non-indexed offsets fail closed with explicit metadata.

## MCP Changes

- Added: none.
- Changed: none to tool names or required parameters.
- Extended: winforms_inspect_control can return AntdUI provider and semantic data through the existing optional provider/semantic sections.
- Unchanged: existing MCP tool count remains 40; no AntdUI-specific MCP tools were added.

## Build

- Stage 4 local Gate passed.
- Format: passed.
- Format verify: passed.
- Restore: passed.
- Release solution build: 0 warnings, 0 errors.
- RendererHost multi-target Release build: 0 warnings, 0 errors.

## Tests

- Full local test run: 348 total, 304 passed, 44 skipped, 0 failed (elevated desktop session).
- New coverage:
  - AntdUI provider detection and fallback behavior.
  - AntdUI Button, Input, InputNumber, Checkbox, Radio, Switch, and Select semantics.
  - Select item semantic children with bounded maxNodes truncation.
  - AntdUI RuntimeBridge E2E through the test app.
  - Managed/UIA correlation fallback for AntdUI controls.
  - UIA text input fallback for controls without direct writable ValuePattern.
  - AntdUI Tabs page selection and bounded paging.
  - AntdUI Tree/Menu hierarchy, selection/state, depth limits, and paging.
  - AntdUI Table columns, row scopes, sorting/filter metadata, row paging, and CellButton semantics.
  - RuntimeBridge semantic-option transport, including null-safe JSON handling.
  - Existing MCP tool surface unchanged at 40 tools.
  - LayeredWindow metadata contract serialization and semantic classification.
  - Select dropdown item bounds/selection/truncation and owner managed ID.
  - Menu popup, Tooltip, Message, and Drawer HWND/owner correlation.

## CI

- PR #1 Core CI: green.
- PR #1 External CI: Claude Code Review fails because the Claude Code GitHub App is not installed on the fork.
- PR #2 Core CI before Stage 4 commit: green on commit 8d66583 feat: add control provider architecture.
- PR #2 Core CI for Stage 4 commit: green on commit b7ac9f2 feat: add AntdUI basic control inspection.
- PR #2 External CI: Claude Code Review fails for the same missing GitHub App setup.
- Stage 4 commit CI: green.
- Stage 5 Core CI: green for commit 700adc8 (push run 32213525287 and PR run 32213528776).
- Stage 6 Core CI: green for commit cbc300f (push run 32216808052 and PR run 32216813261).
- Stage 6 external Claude Code Review: failed because the Claude Code GitHub App is not installed on the fork; no code changes were made for this external-service failure.

## Git

- Base Branch: feature/v11-foundation-refactor.
- Current Branch: feature/v14-antdui-provider.
- Current Head Before Stage 5 Commit: 7869086 docs: record stage 4 ci status.
- Stage 4 Commit: b7ac9f2 feat: add AntdUI basic control inspection.
- Stage 5 Commit: 700adc8 feat: add AntdUI complex semantic inspection.
- Stage 6 Commit: cbc300f `feat: support AntdUI layered windows`.
- Draft PR: #2, target feature/v11-foundation-refactor.
- Working Tree: clean after Stage 6 Core CI status update.

## Risks

- AntdUI repository currently contains untracked .codegraph directories; treat them as local analysis artifacts and never commit them.
- AntdUIProvider intentionally reads only allowlisted public properties and bounded item summaries.
- Provider implementations must continue avoiding arbitrary runtime execution, setters, or method invocation.
- Future AntdUI semantic support for Tabs, Tree, Table, and Menu should stay within the existing provider/semantic architecture and avoid new AntdUI-specific MCP tools unless compatibility requires it.
- Table internals use a narrow allowlist of AntdUI members and return per-scope fallback/diagnostic metadata when version-sensitive caches are unavailable.
- Layered forms are transient and may disappear during enumeration; the inspector
  returns bounded metadata and warnings and tolerates disposal races.
- Local SDK note: this machine lacks the repository-requested .NET 8 SDK, so global.json was temporarily pointed at local .NET 9 for the Gate and restored before commit.

## Next

- Stage 6 Core CI is green; begin Stage 7 Rendering / Theme / DPI.

## Hard Blocker

None.

## Stage 6 Gate Evidence

- `dotnet format Rhombus.WinFormsMcp.sln --verbosity quiet`: passed.
- `dotnet format Rhombus.WinFormsMcp.sln --verify-no-changes --verbosity quiet`: passed.
- `dotnet restore Rhombus.WinFormsMcp.sln`: passed.
- `dotnet build Rhombus.WinFormsMcp.sln --configuration Release --no-restore /m:1 /nr:false`: passed with 0 warnings and 0 errors.
- `dotnet build src/Rhombus.WinFormsMcp.RendererHost/Rhombus.WinFormsMcp.RendererHost.csproj --configuration Release --no-restore /m:1 /nr:false`: passed for net48, netcoreapp3.1, and net8.0-windows with 0 warnings and 0 errors.
- Focused LayeredWindow and popup E2E tests: 18 passed.
- Full elevated desktop test run: 348 total, 304 passed, 44 skipped, 0 failed.
- Non-elevated full test run: one existing FlaUI `SendInput` access-denied failure; elevated rerun passed.

## Stage 5 Gate Evidence

- dotnet format Rhombus.WinFormsMcp.sln --verbosity quiet: passed.
- dotnet format Rhombus.WinFormsMcp.sln --verify-no-changes --verbosity quiet: passed.
- dotnet restore Rhombus.WinFormsMcp.sln: passed.
- dotnet build Rhombus.WinFormsMcp.sln --configuration Release --no-restore /m:1 /nr:false: passed with 0 warnings and 0 errors.
- dotnet build src/Rhombus.WinFormsMcp.RendererHost/Rhombus.WinFormsMcp.RendererHost.csproj --configuration Release --no-restore /m:1 /nr:false: passed with 0 warnings and 0 errors.
- dotnet test Rhombus.WinFormsMcp.sln --configuration Release --no-build: 334 total, 290 passed, 44 skipped, 0 failed.
- GitHub PR #2 Stage 4 CI run 32188783782: passed, build-test-coverage green on Windows.
- GitHub PR #2 Stage 5 push run 32213525287: passed, CI green on Windows.
- GitHub PR #2 Stage 5 synchronize run 32213528776: passed, CI green on Windows.
- GitHub PR #2 Stage 5 status push run 32213834322: passed, CI green on Windows.
- GitHub PR #2 Stage 5 status synchronize run 32213836905: passed, CI green on Windows.
- Focused AntdUIProviderTests: 7 passed.
- Focused RuntimeBridgeLifecycleTests: 14 passed.
- Focused RuntimeInspectionTests: 4 passed.
- One non-elevated desktop E2E attempt was denied by Windows SendInput access; the same E2E and the full Release run passed in the elevated test session. This is an environment permission note, not a product test failure.
