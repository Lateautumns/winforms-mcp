# Compatibility Matrix

This matrix describes the package targets and the evidence available for the
current release-preparation branch. A target marked `designed` is supported by
the project TFM or compatibility mapping, but was not independently executed
in the current environment.

| Area | Target | Status | Evidence / limitation |
| --- | --- | --- | --- |
| MCP Server | .NET 8 Windows | verified | Local Release build/tests and Windows Core CI |
| RuntimeContracts | netstandard2.0 | verified | Local Release build and package preparation |
| RuntimeBridge | .NET Framework 4.8 | verified | Local multi-target Release build |
| RuntimeBridge | .NET 8 Windows | verified | Local Release build/tests and Windows Core CI |
| RendererHost | net48 | verified | Local and Windows multi-target Release build |
| RendererHost | netcoreapp3.1 | verified | Local and Windows multi-target Release build |
| RendererHost | net8.0-windows | verified | Local and Windows multi-target Release build |
| Standard WinForms | .NET 8 test app | verified | RuntimeBridge, UIA, diagnostics, and full test suite |
| AntdUI | AntdUI 2.4.5 test app on .NET 8 | verified | Provider, semantic, popup, rendering, and runtime tests |
| .NET Framework 4.8 application | RuntimeBridge reference | designed | Bridge net48 target; no separate customer application run |
| .NET 10 application | RuntimeBridge reference / renderer mapping | designed | Net8 assemblies and renderer fallback are compatible by design; no .NET 10 SDK run |
| Windows 10 | OS-specific validation | not verified | Current CI reports `windows-latest`, not an isolated Windows 10 run |
| Windows 11 | OS-specific validation | not verified | Current CI reports `windows-latest`, not an isolated Windows 11 run |

The CI workflow is the authoritative Windows check for the commits listed in
the progress log. This document does not claim an OS or SDK test that was not
actually run.
