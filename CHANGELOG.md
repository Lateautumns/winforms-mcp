# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.5.12-beta] - Unreleased

### Added

- Read-only RuntimeBridge identity correlation scoped by process and bridge instance.
- Release-preparation documentation for compatibility, migration, architecture, and local packaging.

### Changed

- Runtime and diagnostics references can carry an optional `bridgeInstanceId` to reject stale application references while preserving legacy clients.
- Local packaging now verifies the server, RuntimeContracts, RuntimeBridge, RendererHost, and NPM distribution artifacts without publishing them.

### Compatibility

- Existing MCP tool names and required parameters remain unchanged.
- RuntimeBridge targets .NET Framework 4.8 and .NET 8 Windows; RendererHost remains multi-targeted for net48, netcoreapp3.1, and net8.0-windows.

### Release status

- This is an unreleased preparation draft. No NuGet push, NPM publish, GitHub Release, or `main` modification is performed.

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
