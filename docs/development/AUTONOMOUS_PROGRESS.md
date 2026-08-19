# Autonomous Progress

## Stage

Stage 12 - Stable release preparation has completed its local and Windows Core CI Gates on `feature/v20-release-prep`; Draft PR #8 is ready for human review.

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
- Extended the existing `winforms_render_form` tool with optional `theme`, `dpi`, and `providerProfile` fields; the Stage 7 tool count remained 40.
- Added request-scoped AntdUI theme/DPI reflection support using the verified `AntdUI.Config.Mode` and `Config.SetDpi(float?)` APIs.
- Restored AntdUI global theme/DPI state after both successful and failed renders, including nested UserControl rendering.
- Added render cache isolation for theme, DPI, provider profile, TFM, and referenced assembly fingerprints.
- Applied bounded logical DPI scaling to the DesignSurface tree for standard WinForms and AntdUI previews.
- Added Stage 7 visual matrix/state restoration tests for AntdUI Button, Input, Tabs, Tree, and Table fixtures at Light/Dark 96/120/144/192 DPI.
- Hardened UIA text input for owner-drawn/composite controls without ValuePattern by using bounded, timeout-protected HWND key/character messages before foreground keyboard fallback; existing AntdUI action E2E now passes reliably.
- Added a shared RuntimeContracts diagnostics model with explicit severity, code, control ID, message, and evidence fields.
- Added bounded layout, DPI, and binding diagnostics sourced from UI-thread managed control snapshots.
- Added deterministic screenshot comparison with PNG/base64 inputs, channel thresholding, changed bounds, bounded tile regions, and cancellation.
- Added read-only accessibility diagnostics with managed AccessibleName/Description, TabIndex/TabStop, UIA correlation, ControlType, and supported patterns.
- Added bounded whitelist-only RuntimeBridge event tracing for Click, TextChanged, CheckedChanged, SelectedIndexChanged, VisibleChanged, EnabledChanged, and FormClosing with ring buffer, cursor paging, expiry, and handler cleanup.
- Added a canonical-root, thread-safe incremental `SourceIndex` for source mapping.
- Indexed namespaces, partial class declarations, Designer fields, `InitializeComponent` references, event registrations, handler methods, and fully qualified symbols.
- Reused unchanged syntax models by path/size/UTC mtime, reparsed changed files, removed deleted files, and preserved the prior committed index when a refresh is cancelled.
- Added optional `maxFiles` to `winforms_get_source_mapping` and read-only scan metadata (`scanned`, `parsed`, `reused`, `removed`, `truncated`, and parse warnings).
- Documented the verified VS MCP navigation/build/debug contract and CodeGraph query contract from the clean local reference repositories.
- Added an optional `SourceIdentitySnapshot` handoff record with absolute editor paths, 1-based spans, project/source-root hints, fully qualified symbols, and runtime control identity.
- Added optional forward-slash `projectRelativeFile` values for CodeGraph disambiguation while retaining all existing absolute source-location fields.
- Added precise optional event-handler locations without changing existing event `file`, `line`, or `fullyQualifiedSymbol` fields.
- Added the cross-MCP workflow to README; WinForms MCP still does not invoke or reference VS MCP or CodeGraph MCP.
- Added a per-host RuntimeBridge instance ID to hello/status and optional request metadata.
- Added connection-scoped instance validation for negotiated clients while preserving legacy clients that do not advertise an instance ID.
- Restricted RuntimeBridge pipe access to the current Windows user on both `net48` and `net8.0-windows`.
- Validated the named-pipe server PID before the MCP Server trusts a RuntimeBridge connection.
- Replaced unbounded line reads with byte-bounded request/response readers and structured oversized-message errors.
- Preserved structured error serialization by emitting an explicit JSON null result.
- Added `Rhombus.WinFormsMcp.UiaWorker`, a restartable out-of-process UIA2 host with a fixed DTO command surface.
- Migrated root-level `winforms_element_exists` and `winforms_wait_for_element` probes to the worker without changing either Tool contract.
- Added bounded worker request/response transport, startup/request timeouts, timeout Kill, next-call recreation, stderr diagnostics, and deterministic disposal.
- Kept hidden-desktop automation on the existing desktop-aware in-process path and added an explicit compatibility fallback when the worker binary is unavailable.
- Stabilized the AntdUI layered Tooltip test fixture by invoking its existing `NoMessage()` mode before showing it, so enumeration cannot close the fixture during the bounded correlation window.

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
- Rendering remains isolated in RendererHost; no AntdUI compile-time reference was added to Rendering, Server, RuntimeBridge, or RuntimeContracts.
- Diagnostics remain generic and provider-independent; RuntimeBridge still exposes no setters, Method.Invoke, business method execution, or reflection execution surface.
- Runtime event trace sessions own their subscriptions, have bounded lifetime/capacity, and are removed on Stop, expiry, session pressure, and host disposal.
- SourceIndex state is isolated per canonical source root, serialized per root, bounded to a fixed number of roots/files, and never exposes Roslyn syntax objects through MCP.
- Cross-MCP integration is metadata-only: no client, HTTP transport, project reference, package reference, or copied source was added for VS MCP or CodeGraph.
- RuntimeBridge IPC remains local-only and read-only. New clients negotiate a per-instance nonce; older Protocol v1 clients retain a no-nonce compatibility path under the same-user pipe ACL.
- UIA Worker requests contain only command DTOs and primitive JSON. `AutomationElement`, COM wrappers, and live UI objects never cross the process boundary.

## MCP Changes

- Added: `winforms_detect_layout_issues`, `winforms_compare_screenshot`, `winforms_check_accessibility`, `winforms_start_event_trace`, `winforms_read_event_trace`, `winforms_stop_event_trace`.
- Changed: `winforms_render_form` only by adding optional `theme`, `dpi`, and `providerProfile` parameters; `winforms_get_source_mapping` adds optional `maxFiles`; no required parameter changed.
- Extended: `winforms_get_source_mapping` adds optional source identity, project-relative paths, and precise handler locations; existing fields remain compatible.
- Extended: winforms_inspect_control can return AntdUI provider and semantic data through the existing optional provider/semantic sections.
- Unchanged: all existing 40 tool names and required parameters remain compatible; the six additions are generic diagnostics and do not add AntdUI-specific tools.
- Stage 11 IPC hardening adds no MCP tool and changes no required tool parameter.
- Stage 11 UIA isolation reuses the existing `element_exists` and `wait_for_element` Tools; the registry remains at 46 Tools.

## Build

- Stage 11 IPC hardening and initial UIA Worker local Gates passed.
- Format: passed.
- Format verify: passed.
- Restore: passed.
- Release solution build: 0 warnings, 0 errors.
- RendererHost multi-target Release build: 0 warnings, 0 errors.

## Tests

- Full local Stage 8 test run: 379 total, 335 passed, 44 skipped, 0 failed (elevated desktop session).
- Full local Stage 11 UIA Worker Release test run before the final fixture rebuild: 402 total, 358 passed, 44 skipped, 0 failed.
- Rebuilt layered-window fixture and ran the four-case suite for five consecutive rounds: 20 passed, 0 failed.
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
  - Existing MCP tool surface remains compatible; total registry surface is now 46 tools.
  - LayeredWindow metadata contract serialization and semantic classification.
  - Select dropdown item bounds/selection/truncation and owner managed ID.
  - Menu popup, Tooltip, Message, and Drawer HWND/owner correlation.
  - Render visual option normalization, cache isolation, standard WinForms DPI scaling.
  - AntdUI Light/Dark rendering matrix at 96/120/144/192 DPI and global-state restoration on success/failure.
  - Layout/DPI/binding diagnostic evidence and maxDiagnostics bounds.
  - Deterministic screenshot diff pixels, bounds, tile regions, thresholds, and invalid input handling.
  - Accessibility diagnostic bounds and managed/UIA enrichment.
  - Runtime event trace ring buffer, sequence paging, expiry, Stop cleanup, and real TestApp Named Pipe capture.

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
- Stage 7 Core CI: green for commit f3bf321 (push run 32221982955 and PR run 32221986299).
- Stage 7 external Claude Code Review: failed with 401 because the Claude Code GitHub App is not installed on this fork; no code changes were made for this external-service failure.
- Stage 8 Core CI: green for commit cd7ef0e (push run 32236157363 and PR run 32236160606).
- Stage 8 external Claude Code Review run 32236160617 failed with 401 because the GitHub App is not installed on this fork; no code changes were made for this external-service failure.
- Stage 8 CI status commit c844c1b: Core CI green (push run 32236718177 and PR run 32236723237); external Claude Code Review run 32236723248 failed with the same missing-App 401.
- Stage 9 CI status commit 72ab00f: Core CI green (push run 32243431847 and PR run 32243436411); external Claude Code Review run 32243436427 failed with the same missing-App 401.
- Stage 10 Core CI: green for commit ea615d9 (push run 32246092879 and PR run 32246197318).
- Stage 10 external Claude Code Review run 32246197161 failed because the Claude Code GitHub App is not installed on this fork; no code changes were made for this external-service failure.
- Stage 11 UIA Worker Windows Core CI: pending commit and push.
- Stage 11 IPC hardening Core CI is green for commit `feaf781`: push run `32248894192` and PR run `32248925239`.
- Stage 11 external Claude Code Review run `32248925255` failed because the GitHub App is not installed on the fork; no code change was made for this external-service failure.

## Git

- Base Branch: feature/v17-contract-analysis.
- Current Branch: feature/v18-hardening.
- Stage 11 IPC Commit: feaf781 `feat: harden runtime bridge ipc security`.
- Draft PR: #6 targets `feature/v17-contract-analysis` with head `feature/v18-hardening`.
- Stage 7 Commit: f3bf321 `feat: support render theme and dpi profiles`.
- Stage 8 Commit: cd7ef0e `feat: add WinForms runtime diagnostics`.
- Stage 8 CI Status Commit: c844c1b `docs: record stage 8 ci status`.
- Current Head before the Stage 9 start commit: c844c1b.
- Stage 4 Commit: b7ac9f2 feat: add AntdUI basic control inspection.
- Stage 5 Commit: 700adc8 feat: add AntdUI complex semantic inspection.
- Stage 6 Commit: cbc300f `feat: support AntdUI layered windows`.
- Draft PR: #3 targets feature/v14-antdui-provider; Draft PR #4 targets feature/v15-diagnostics with head `feature/v16-source-index`.
- Draft PR #5 targets `feature/v16-source-index` with head `feature/v17-contract-analysis`.
- Working Tree: the initial UIA Worker isolation slice and Tooltip fixture hardening passed the final local Gate and are ready for commit.

## Risks

- AntdUI repository currently contains untracked .codegraph directories; treat them as local analysis artifacts and never commit them.
- AntdUIProvider intentionally reads only allowlisted public properties and bounded item summaries.
- Provider implementations must continue avoiding arbitrary runtime execution, setters, or method invocation.
- Future provider extensions should stay within the existing provider/semantic architecture and avoid AntdUI-specific MCP tools unless compatibility requires it.
- Table internals use a narrow allowlist of AntdUI members and return per-scope fallback/diagnostic metadata when version-sensitive caches are unavailable.
- Layered forms are transient and may disappear during enumeration; the inspector
  returns bounded metadata and warnings and tolerates disposal races.
- Local SDK: repository-requested .NET 8.0.424 is installed and `global.json` remains unchanged.
- Protocol v1 legacy clients can omit the instance ID; same-user ACL and PID validation remain enforced, while negotiated clients require the current instance ID after hello.
- UIA isolation is intentionally incremental: root-level existence/wait probes are isolated now; operations that return or consume cached live `AutomationElement` instances remain in process until locator/reference rehydration is introduced.

## Next

- Commit and push the initial UIA Worker isolation slice to PR #6, then wait for Windows Core CI.
- Continue Stage 11 with multi-process runtime identity and the remaining resource-lifecycle audit.

## Stage 10 Gate Evidence

- Reference repositories remained clean/read-only: VS-MCPServer `main` at `1d020ae`; CodeGraph `main` at `c6aaa20`.
- `dotnet format Rhombus.WinFormsMcp.sln --verbosity quiet`: passed.
- `dotnet format Rhombus.WinFormsMcp.sln --verify-no-changes --verbosity quiet`: passed.
- `dotnet restore Rhombus.WinFormsMcp.sln`: passed.
- `dotnet build Rhombus.WinFormsMcp.sln --configuration Release --no-restore /m:1 /nr:false`: passed with 0 warnings and 0 errors.
- `dotnet build src/Rhombus.WinFormsMcp.RendererHost/Rhombus.WinFormsMcp.RendererHost.csproj --configuration Release --no-restore /m:1 /nr:false`: passed for net48, netcoreapp3.1, and net8.0-windows with 0 warnings and 0 errors.
- Focused source identity/source mapping tests: 7 passed.
- Full Release test run: 385 total, 341 passed, 44 skipped, 0 failed.

## Stage 11 IPC Hardening Gate Evidence

- RuntimeBridge lifecycle/IPC focused tests: 20 passed, 0 skipped, 0 failed.
- `dotnet format Rhombus.WinFormsMcp.sln --verbosity quiet`: passed.
- `dotnet format Rhombus.WinFormsMcp.sln --verify-no-changes --verbosity quiet`: passed.
- `dotnet restore Rhombus.WinFormsMcp.sln`: passed.
- `dotnet build Rhombus.WinFormsMcp.sln --configuration Release --no-restore /m:1 /nr:false`: passed with 0 warnings and 0 errors.
- `dotnet build src/Rhombus.WinFormsMcp.RendererHost/Rhombus.WinFormsMcp.RendererHost.csproj --configuration Release --no-restore /m:1 /nr:false`: passed for net48, netcoreapp3.1, and net8.0-windows with 0 warnings and 0 errors.
- Full Release test run: 391 total, 347 passed, 44 skipped, 0 failed.
- No MCP tool was added; the registry remains at 46 tools.

## Stage 11 UIA Worker Gate Evidence

- Added a real worker process and migrated two root-level UIA query paths; no `AutomationElement` is serialized.
- Focused UIA Worker/configuration/official MCP SDK tests: 34 passed, 0 skipped, 0 failed.
- Worker lifecycle coverage includes handshake, isolated query, concurrent reuse, timeout Kill/recreate, active-request disposal, headless fallback, and no orphan process.
- `dotnet format Rhombus.WinFormsMcp.sln --verbosity quiet`: passed.
- `dotnet format Rhombus.WinFormsMcp.sln --verify-no-changes --verbosity quiet`: passed.
- `dotnet restore Rhombus.WinFormsMcp.sln`: passed.
- `dotnet build Rhombus.WinFormsMcp.sln --configuration Release --no-restore /m:1 /nr:false`: passed with 0 warnings and 0 errors.
- `dotnet build src/Rhombus.WinFormsMcp.RendererHost/Rhombus.WinFormsMcp.RendererHost.csproj --configuration Release --no-restore /m:1 /nr:false`: passed for net48, netcoreapp3.1, and net8.0-windows with 0 warnings and 0 errors.
- Full Release test rerun before the final fixture rebuild: 402 total, 358 passed, 44 skipped, 0 failed.
- Final full Release test after the fixture rebuild: 403 total, 359 passed, 44 skipped, 0 failed.
- The layered-window suite passed 20/20 across five consecutive rounds after the fixture calls AntdUI's existing `NoMessage()` mode; no assertion was weakened and no test was skipped.

## Stage 9 Scope

- Index source roots, namespaces, partial types, Designer fields and initialization, event registrations, handler methods, and fully qualified symbols.
- Cache per-file parse results by path, size, and modification time, with optional content hashing where timestamp precision is insufficient.
- Reuse unchanged parse results and invalidate only changed/deleted files.
- Keep scans bounded by max files, cancellation, and the existing tool timeout pipeline.

## Hard Blocker

None.

## Stage 9 Gate Evidence

- Repository SDK: .NET 8.0.424; `global.json` unchanged and clean.
- `dotnet format Rhombus.WinFormsMcp.sln --verbosity quiet`: passed.
- `dotnet format Rhombus.WinFormsMcp.sln --verify-no-changes --verbosity quiet`: passed.
- `dotnet restore Rhombus.WinFormsMcp.sln`: passed.
- `dotnet build Rhombus.WinFormsMcp.sln --configuration Release --no-restore /m:1 /nr:false`: passed with 0 warnings and 0 errors.
- `dotnet build src/Rhombus.WinFormsMcp.RendererHost/Rhombus.WinFormsMcp.RendererHost.csproj --configuration Release --no-restore /m:1 /nr:false`: passed for net48, netcoreapp3.1, and net8.0-windows with 0 warnings and 0 errors.
- Focused SourceIndex/source-mapping tests: 6 passed.
- Full Release test run: 384 total, 340 passed, 44 skipped, 0 failed.
- Core CI: green for commit `5779490` on both push run `32242797084` and pull-request run `32242800459`.
- External Claude Code Review run `32242800339`: failed with 401 because the Claude Code GitHub App is not installed on the fork; this is non-blocking and required no code change.

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

## Stage 7 Gate Evidence

- `dotnet format Rhombus.WinFormsMcp.sln --verbosity quiet`: passed.
- `dotnet format Rhombus.WinFormsMcp.sln --verify-no-changes --verbosity quiet`: passed.
- `dotnet restore Rhombus.WinFormsMcp.sln`: passed.
- `dotnet build Rhombus.WinFormsMcp.sln --configuration Release --no-restore /m:1 /nr:false`: passed with 0 warnings and 0 errors.
- `dotnet build src/Rhombus.WinFormsMcp.RendererHost/Rhombus.WinFormsMcp.RendererHost.csproj --configuration Release --no-restore /m:1 /nr:false`: passed for net48, netcoreapp3.1, and net8.0-windows with 0 warnings and 0 errors.
- Focused Stage 7 RuntimeBridge, AntdUI provider, rendering, and renderer-pool tests: 33 passed.
- Full elevated Release test run: 369 total, 325 passed, 44 skipped, 0 failed.
- The existing AntdUI owner-drawn Input UIA action now passes using a bounded HWND WM_KEY/WM_CHAR fallback when ValuePattern is unavailable.

## Stage 8 Gate Evidence

- `dotnet format Rhombus.WinFormsMcp.sln --verbosity quiet`: passed.
- `dotnet format Rhombus.WinFormsMcp.sln --verify-no-changes --verbosity quiet`: passed.
- `dotnet restore Rhombus.WinFormsMcp.sln`: passed.
- `dotnet build Rhombus.WinFormsMcp.sln --configuration Release --no-restore /m:1 /nr:false`: passed with 0 warnings and 0 errors.
- `dotnet build src/Rhombus.WinFormsMcp.RendererHost/Rhombus.WinFormsMcp.RendererHost.csproj --configuration Release --no-restore /m:1 /nr:false`: passed for net48, netcoreapp3.1, and net8.0-windows with 0 warnings and 0 errors.
- Full elevated Release test run: 379 total, 335 passed, 44 skipped, 0 failed.
- Windows Core CI passed for feature commit cd7ef0e: push run 32236157363 and pull_request run 32236160606.
- External Claude Code Review run 32236160617 failed with the known missing-GitHub-App 401 and is not a Core CI failure.

## Stage 11 Runtime Identity Gate Evidence

- Runtime-scoped identities now carry `processId` and `bridgeInstanceId` across managed control summaries, ancestors, layered-window ownership metadata, source mapping, diagnostics, and event-trace snapshots.
- Existing runtime/diagnostics Tools keep all prior required parameters and accept one optional `bridgeInstanceId`; the Tool registry remains at 46 Tools.
- The named-pipe client performs hello negotiation for every runtime request and rejects an expected stale instance before sending the command. Legacy clients that omit the optional identity remain compatible.
- Runtime identity tests cover weak control lifetime, host stale-instance rejection, client-side stale-instance rejection, legacy fallback, concurrent shutdown, and output context propagation.
- `dotnet format Rhombus.WinFormsMcp.sln --verbosity quiet`: passed.
- `dotnet format Rhombus.WinFormsMcp.sln --verify-no-changes --verbosity quiet`: passed.
- `dotnet restore Rhombus.WinFormsMcp.sln`: passed.
- `dotnet build Rhombus.WinFormsMcp.sln --configuration Release --no-restore /m:1 /nr:false`: passed with 0 warnings and 0 errors.
- `dotnet build src/Rhombus.WinFormsMcp.RendererHost/Rhombus.WinFormsMcp.RendererHost.csproj --configuration Release --no-restore /m:1 /nr:false`: passed for net48, netcoreapp3.1, and net8.0-windows with 0 warnings and 0 errors.
- Focused identity/lifecycle/inspection/source-index/diagnostics tests: 48 passed, 0 failed.
- Full Release test run: 406 total, 362 passed, 44 skipped, 0 failed.
- AntdUI reference repository remained read-only; its pre-existing untracked `.codegraph` analysis artifacts were not touched or committed.

## Stage 12 Release Preparation Scope

- Release preparation is local-only: package manifests, compatibility documentation, migration/release notes, and reproducible local package/ZIP checks are allowed.
- No NuGet push, NPM publish, GitHub release, or modification of `main` is permitted during unattended execution.
- Compatibility claims will distinguish locally verified targets from Windows/OS combinations not available in the current environment.

## Stage 12 Gate Evidence

- Gate timestamp: 2026-08-19 21:40:12 +08:00.
- Branch: `feature/v20-release-prep`, based on `a35e7db` / `feature/v19-runtime-identity`.
- Added package metadata and README inclusion for RuntimeContracts and RuntimeBridge.
- Added local-only `scripts/package-local.ps1` checks for three NuGet packages, one NPM tarball, and the standalone ZIP with all RendererHost targets.
- Added compatibility matrix, migration guide, release notes draft, release architecture, README release-preparation guidance, and `1.5.12-beta` changelog entry.
- `dotnet format Rhombus.WinFormsMcp.sln`: passed.
- `dotnet format Rhombus.WinFormsMcp.sln --verify-no-changes`: passed.
- `dotnet restore Rhombus.WinFormsMcp.sln`: passed.
- `dotnet build Rhombus.WinFormsMcp.sln --configuration Release --no-restore /m:1 /nr:false`: passed with 0 warnings and 0 errors.
- `dotnet build src/Rhombus.WinFormsMcp.RendererHost/Rhombus.WinFormsMcp.RendererHost.csproj --configuration Release --no-restore /m:1 /nr:false`: passed for net48, netcoreapp3.1, and net8.0-windows with 0 warnings and 0 errors.
- Full Release test run: 406 total, 362 passed, 44 skipped, 0 failed.
- `scripts/package-local.ps1 -Configuration Release`: passed; all expected NuGet, NPM, and ZIP artifact assertions succeeded without publishing.
- AntdUI reference repository remained read-only; no `.codegraph` artifacts or build outputs were added to Git.
- Commit: `edce1dd` (`chore: prepare local 1.5.12-beta release`).
- Draft PR: #8, `feature/v20-release-prep` -> `feature/v19-runtime-identity`.
- Windows Core CI: green for the pushed head `24a84ec` (push run `32259744121`, pull-request run `32259755640`).
- External Claude Code Review run `32259755616` failed with 401 because the Claude Code GitHub App is not installed on this fork; no code change was made for this external-service failure.

## Release Candidate Validation

- Current phase: Release Candidate Validation, branch `release/v1.0.0-rc1`,
  based on `feature/v20-release-prep` at `7bbd2b0`.
- Added the frozen API reference at `docs/MCP-API.md` and the candidate gate at
  `docs/release/v1.0.0-rc1-checklist.md`. The API inventory matches the 46
  definitions in `ToolNames`/`ToolDefinitionCatalog`.
- Real project target: read-only `D:\02_工作\在研项目\NGUS2`, project
  `NGUS2\NGUS2.csproj`, existing `NGUSV3.2.exe`, `.NET Framework 4.7.2`,
  AnyCPU, AntdUI 2.4.x.
- UIA validation passed against a disposable copy of the release output:
  attach, process status, one-window enumeration, 48-node element tree, a real
  property read, window screenshot, unchanged screenshot diff, and a cached UIA
  tab interaction. The original business repository was not modified.
- RuntimeBridge status correctly degraded to a structured unavailable error for
  NGUS2 because the current bridge targets `net48` and `net8.0-windows`; managed
  tree, source mapping, and RuntimeBridge diagnostics remain unverified for this
  target and are recorded as a compatibility limitation.
- Real AntdUI rendering initially exposed two renderer dependency gaps:
  legacy projects emitted assemblies directly in `bin\Release`, and the main
  application assembly was an `.exe`. `FormRenderingHelpers` now considers
  direct configuration output, DLL/EXE assemblies, and prefers the most
  complete candidate directory. A regression test covers Debug-only EXE versus
  Release DLL/EXE output.
- After the fix, `winforms_render_form` rendered NGUS2 `MainForm.Designer.cs`
  with AntdUI/Light/96 DPI successfully. The resulting PNG was 30,911 bytes and
  no longer contained `Type not found` placeholders for NGUS2 custom controls.
- Focused regression gate after the fix: `FormRenderingHelpersTests` 13 passed,
  0 failed.
- RC local gate completed at `2026-08-19 23:14:16 +08:00`: format and
  verify-no-changes passed; restore passed; solution Release build passed with
  0 warnings/errors; RendererHost `net48`, `netcoreapp3.1`, and
  `net8.0-windows` build passed with 0 warnings/errors; full Release tests
  passed at 407 total, 363 passed, 44 skipped, 0 failed.
- Local package validation passed after the Windows ZIP path assertion was
  normalized. It generated the three NuGet packages, one NPM tarball, and the
  standalone ZIP in a temporary directory without publishing.
- Problems found: RuntimeBridge TFM mismatch and nested UIA desktop-query
  limitation; neither justified changing the protocol or adding a new tool.
- Draft PR #9 targets `feature/v20-release-prep`; Windows Core CI passed for
  the final RC validation content (run `32270980161`).
- External Claude Code Review run `32270980234` failed with the known 401
  because the Claude Code GitHub App is not installed on this fork; this is
  non-blocking and caused no code change.
- RC validation is complete. Remaining work is human acceptance of the
  documented NGUS2 net472 RuntimeBridge limitation and approval before any
  `v1.0.0` tag or package/release publication. Do not modify `main`, publish
  packages, or touch NGUS2/AntdUI source repositories.
