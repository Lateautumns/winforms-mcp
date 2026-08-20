# Compatibility Matrix

This matrix describes the package targets and the evidence available for the
current release-preparation branch. A target marked `designed` is supported by
the project TFM or compatibility mapping, but was not independently executed
in the current environment.

| Area | Target | Status | Evidence / limitation |
| --- | --- | --- | --- |
| MCP Server | .NET 8 Windows | verified | Local Release build/tests and Windows Core CI |
| RuntimeContracts | netstandard2.0 | verified | Local Release build, package preparation, and package asset check (netstandard2.0 only) |
| RuntimeBridge | .NET Framework 4.7.2 (compile target) | verified | net472 Release build and two real net472 consumers (SDK-style and legacy non-SDK) restore, build, launch, and return real control trees over Protocol v1 |
| RuntimeBridge | .NET Framework 4.8 | verified | Local multi-target Release build |
| RuntimeBridge | .NET 8 Windows | verified | Local Release build/tests and Windows Core CI |
| RuntimeBridge on .NET Framework CLR | runtime CLR actually executed | verified locally; hosted CI pending | Local consumer E2E runs on the machine's installed .NET Framework CLR (currently 4.8.x); the hosted Windows workflow is configured but has not yet produced evidence for this branch. Assertions only check the runtime is ".NET Framework", never a pinned 4.8 revision |
| RuntimeBridge on a bare 4.7.2-only machine | runtime CLR 4.7.2 only | not verified | Not claimed: no standalone machine with only the original 4.7.2 runtime has been exercised |
| RendererHost | net48 | verified | Local and Windows multi-target Release build |
| RendererHost | netcoreapp3.1 | verified | Local and Windows multi-target Release build |
| RendererHost | net8.0-windows | verified | Local and Windows multi-target Release build |
| Standard WinForms | .NET 8 test app | verified | RuntimeBridge, UIA, diagnostics, and full test suite |
| AntdUI | AntdUI 2.4.5 test app on .NET 8 | verified | Provider, semantic, popup, rendering, and runtime tests |
| .NET Framework 4.7.2 application | RuntimeBridge reference | verified | Both consumer projects reference the packed `Rhombus.WinFormsMcp.RuntimeBridge` package via `PackageReference` only |
| .NET Framework 4.8 application | RuntimeBridge reference | designed | Bridge net48 target; no separate customer application run |
| .NET 10 application | RuntimeBridge reference / renderer mapping | designed | Net8 assemblies and renderer fallback are compatible by design; no .NET 10 SDK run |
| Windows 10 | OS-specific validation | not verified | Current CI reports `windows-latest`, not an isolated Windows 10 run |
| Windows 11 | OS-specific validation | not verified | Current CI reports `windows-latest`, not an isolated Windows 11 run |

## Compile target vs. runtime CLR

- The consumers compile against the .NET Framework 4.7.2 targeting pack
  (`TargetFrameworkVersion=v4.7.2`), which is what "compiles for .NET Framework
  4.7.2" means here.
- At runtime the process executes on the .NET Framework CLR installed on the
  machine that runs the test. The current evidence is a local run on 4.8.x;
  when hosted CI runs, its installed CLR may also be newer than 4.7.2.
- This document does **not** claim that the packages were verified on a machine
  where only the original 4.7.2 runtime is installed.

The CI workflow becomes authoritative hosted-Windows evidence after it has run
successfully for the relevant commit. This document does not claim an OS, CLR,
or SDK test that was not actually run.
