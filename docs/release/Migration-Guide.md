# Migration Guide

## From UIA-only automation

Existing UIA tools remain compatible. Keep using `find_element`,
`get_element_tree`, and the interaction tools for actions. RuntimeBridge is an
optional read-only understanding layer; an application without it continues to
work through UIA.

To enable managed inspection, reference the bridge package in the target
WinForms application (`Rhombus.WinFormsMcp.RuntimeContracts` is pulled in
transitively; referencing it explicitly is optional but keeps the version
visible):

```xml
<PackageReference Include="Rhombus.WinFormsMcp.RuntimeBridge" Version="1.5.12-beta" />
```

.NET Framework 4.7.2, 4.8, and .NET 8 Windows applications can all reference
the same package; NuGet selects the matching `net472`, `net48`, or
`net8.0-windows` asset automatically.

Start the bridge from `Form.Shown` (the window handle exists at that point) and
stop it during form shutdown:

```csharp
form.Shown += (_, _) => McpRuntimeBridge.StartForControl(form);
form.FormClosed += (_, _) => McpRuntimeBridge.Stop();
```

`StartForControl(form, options)` binds the bridge to a specific control:

- `null` throws `ArgumentNullException`.
- A disposed/disposing control throws `ObjectDisposedException`.
- A control without a window handle throws `InvalidOperationException` whose
  message suggests calling from `Form.Shown`.
- The first successful start binds the control; later calls return the existing
  host and never change the dispatch target.

The legacy `McpRuntimeBridge.Start(options)` entry point is still source- and
binary-compatible. When no host is running it only accepts an open form or a
confirmable WinForms UI synchronization context; without either it throws
`InvalidOperationException` immediately (with a `StartForControl` migration
example) instead of touching controls from the pipe thread. The bridge never
falls back to cross-thread control access: requests without a UI dispatch
target, or with an invalidated bound control, fail explicitly.

### Legacy (non-SDK) .NET Framework projects

Traditional `.csproj` projects that reference the bridge package with
`PackageReference` should enable automatic binding redirects so the
`System.Text.Json` dependency closure resolves at runtime:

```xml
<PropertyGroup>
  <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
  <GenerateBindingRedirectsOutputType>true</GenerateBindingRedirectsOutputType>
</PropertyGroup>
```

SDK-style projects should set the same properties for .NET Framework targets.
The repository verifies both project shapes against the packed package
(`scripts/verify-net472-consumers.ps1`).

The bridge only returns snapshot DTOs. It does not expose setters, arbitrary
method invocation, reflection execution, or business objects across the pipe.

## Runtime identity

Managed IDs such as `ctrl_18` are scoped to a process and bridge instance. Keep
the `processId` and `bridgeInstanceId` returned by `runtime_status` or a
managed identity together with the ID. Pass `bridgeInstanceId` to existing
runtime and diagnostics tools when replaying a saved reference. A mismatch
means the application restarted; refresh the managed tree instead of retrying a
stale control or event trace ID.

The field is optional for compatibility with older clients and older bridges.
Omitting it preserves the legacy behavior, while supplying it enables strict
stale-reference protection.

## Target frameworks

`Rhombus.WinFormsMcp.RuntimeContracts` is a single-target `netstandard2.0`
assembly, so .NET Framework 4.7.2/4.8 and .NET 8 consumers share one contracts
DLL. RuntimeBridge targets `net472`, `net48`, and `net8.0-windows`. The server
itself targets `net8.0-windows`; RendererHost is multi-targeted for `net48`,
`netcoreapp3.1`, and `net8.0-windows`.

Compile target and runtime CLR are distinct: the consumers compile against the
4.7.2 targeting pack, but the machine that runs them executes the installed
.NET Framework CLR (typically 4.8.x). Support on a machine with only the
original 4.7.2 runtime installed is not claimed; see the
[compatibility matrix](Compatibility-Matrix.md).

## Release preparation

Run `scripts/package-local.ps1` on Windows after a Release build to create
local NuGet packages, an NPM tarball, and a standalone ZIP. The script never
publishes packages or creates a GitHub release. `scripts/pack-nuget.ps1` packs
and checks the three NuGet packages (names, versions, TFM assets, inter-project
dependency versions) and is shared by local packaging, CI, and release
workflows. `scripts/verify-net472-consumers.ps1` runs the two .NET Framework
4.7.2 consumers end to end against the freshly packed package.
