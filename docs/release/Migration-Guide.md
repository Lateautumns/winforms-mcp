# Migration Guide

## From UIA-only automation

Existing UIA tools remain compatible. Keep using `find_element`,
`get_element_tree`, and the interaction tools for actions. RuntimeBridge is an
optional read-only understanding layer; an application without it continues to
work through UIA.

To enable managed inspection, reference these packages in the target WinForms
application:

```xml
<PackageReference Include="Rhombus.WinFormsMcp.RuntimeContracts" Version="1.5.12-beta" />
<PackageReference Include="Rhombus.WinFormsMcp.RuntimeBridge" Version="1.5.12-beta" />
```

Start the bridge from the UI thread during development and stop it during form
shutdown:

```csharp
form.Shown += (_, _) => McpRuntimeBridge.Start();
form.FormClosed += (_, _) => McpRuntimeBridge.Stop();
```

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

RuntimeContracts targets `netstandard2.0`. RuntimeBridge targets `net48` and
`net8.0-windows`. The server itself targets `net8.0-windows`; RendererHost is
multi-targeted for `net48`, `netcoreapp3.1`, and `net8.0-windows`.

## Release preparation

Run `scripts/package-local.ps1` on Windows after a Release build to create
local NuGet packages, an NPM tarball, and a standalone ZIP. The script never
publishes packages or creates a GitHub release.
