# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.5.12-beta] - Unreleased

### Added

- Read-only RuntimeBridge identity correlation scoped by process and bridge instance.
- Official .NET Framework 4.7.2 support: `Rhombus.WinFormsMcp.RuntimeBridge` now targets `net472;net48;net8.0-windows`, with a new `McpRuntimeBridge.StartForControl(Control, RuntimeBridgeOptions?)` entry point that binds the bridge to a control from `Form.Shown` and validates the control before starting.
- Two minimal .NET Framework 4.7.2 consumers (SDK-style and legacy non-SDK projects) that reference only the packed `Rhombus.WinFormsMcp.RuntimeBridge` package via `PackageReference`; `scripts/verify-net472-consumers.ps1` restores, builds, launches, and verifies both over Protocol v1 end to end.
- Unified packaging (`scripts/pack-nuget.ps1`) and version-sync (`scripts/sync-version.ps1`) scripts shared by local packaging, CI, and release workflows.
- Release-preparation documentation for compatibility, migration, architecture, and local packaging.

### Changed

- Runtime and diagnostics references can carry an optional `bridgeInstanceId` to reject stale application references while preserving legacy clients.
- Local packaging now verifies the server, RuntimeContracts, RuntimeBridge, RendererHost, and NPM distribution artifacts without publishing them.
- `McpRuntimeBridge.Start()` keeps source/binary compatibility but fails fast with a `StartForControl` migration hint when no open form and no WinForms UI synchronization context is available; the UI dispatcher never falls back to cross-thread control access and fails requests explicitly when the bound control is invalid.
- Both TestApps now start the bridge with `StartForControl(form)` in `Form.Shown` and stop it in `FormClosed`.
- `global.json` pins SDK `8.0.100` with `rollForward=latestFeature`; CI and release workflows install the pinned SDK, configure Visual Studio MSBuild, and run the three-package check plus the two net472 consumer smoke tests as release gates.

### Compatibility

- Existing MCP tool names and required parameters remain unchanged; Protocol v1, `Stop`, and `StopAsync` are unchanged.
- `Rhombus.WinFormsMcp.RuntimeContracts` stays a single-target `netstandard2.0` assembly; `Rhombus.WinFormsMcp.RuntimeBridge` targets `net472`, `net48`, and `net8.0-windows`; RendererHost remains multi-targeted for net48, netcoreapp3.1, and net8.0-windows.
- Compile target and runtime CLR are distinct: consumers compile against the 4.7.2 targeting pack, while current runtime evidence is a local E2E run on the installed CLR (4.8.x). Hosted CI is configured but not yet evidenced for this branch; verification on a machine with only the original 4.7.2 runtime is not claimed.

### Release status

- This is an unreleased preparation draft. No NuGet push, NPM publish, GitHub Release, commit, tag, or `main` modification is performed. When released, this backwards-compatible feature ships as a SemVer minor bump.

## [1.0.0] - 2024-10-21

### Added
- Initial release of Rhombus.WinFormsMcp
- WinForms automation MCP server using FlaUI with UIA2 backend
- Headless UI automation capabilities for CI/CD environments
- Element discovery by AutomationId, Name, ClassName, and ControlType
- UI interaction methods: click, typing, value setting, drag-drop
- Process lifecycle management (launch, attach, close)
- Screenshot capture and visual validation
- Full async/await support for modern .NET applications
- Comprehensive mock-based test suite (52+ passing tests)
- NuGet package: `Rhombus.WinFormsMcp`
- NPM package: `@fnrhombus/winforms-mcp` with npx support
- GitHub Actions CI/CD workflows
- Multi-platform publishing (NuGet, NPM, GitHub Releases)
- Branch protection and pull request workflows
- MIT License

### Features
- **Automated Element Discovery**: Find UI elements by various properties
- **Headless Operation**: No display server or GUI interaction required
- **Full Process Control**: Launch, attach, and manage application lifecycles
- **Visual Validation**: Capture screenshots for analysis
- **Async Integration**: Seamless integration with modern .NET async patterns
- **Cross-Platform Distribution**: Available via NuGet, NPM, and direct download

### Testing
- 52+ passing unit-level tests
- 24+ integration-level tests
- 19+ end-to-end tests
- Comprehensive negative test coverage for error scenarios
- Test coverage includes error recovery and fallback patterns

### Known Limitations
- Windows-only (requires x64 architecture)
- Requires .NET 8.0 runtime or SDK
- UI automation limited to Win32 UI framework (WinForms, WPF, native Windows)

---

For more information, visit [https://github.com/fnrhombus/winforms-mcp](https://github.com/fnrhombus/winforms-mcp)
