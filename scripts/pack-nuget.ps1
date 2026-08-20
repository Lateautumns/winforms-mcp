param(
    [string]$Configuration = "Release",
    [string]$Version = "",
    [string]$OutputRoot = ""
)

# Unified NuGet packaging for the three publishable packages:
# Rhombus.WinFormsMcp (Server), Rhombus.WinFormsMcp.RuntimeContracts,
# Rhombus.WinFormsMcp.RuntimeBridge. Used by local packaging
# (package-local.ps1), the .NET Framework 4.7.2 consumer verification script,
# and the beta/stable release workflows. This script never publishes.

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Content (Join-Path $repoRoot "VERSION") -Raw).Trim()
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path ([IO.Path]::GetTempPath()) "winforms-mcp-package-$([Guid]::NewGuid().ToString('N'))"
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$nugetRoot = Join-Path $OutputRoot "nuget"
New-Item -ItemType Directory -Path $nugetRoot -Force | Out-Null

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Get-ZipEntryNames([string]$packagePath) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        return @($archive.Entries | ForEach-Object FullName)
    }
    finally {
        $archive.Dispose()
    }
}

function Get-Nuspec([string]$packagePath) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        $entry = $archive.Entries | Where-Object { $_.FullName -like "*.nuspec" } | Select-Object -First 1
        if ($null -eq $entry) {
            throw "Package $packagePath contains no nuspec."
        }
        $reader = New-Object System.IO.StreamReader($entry.Open())
        try {
            return [xml]$reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Get-NuspecDependencies([xml]$nuspec) {
    $dependencies = @()
    $dependenciesNode = $nuspec.package.metadata.dependencies
    if ($null -eq $dependenciesNode) {
        return $dependencies
    }

    $groups = @($dependenciesNode.group)
    if ($groups.Count -eq 0 -and $null -ne $dependenciesNode.dependency) {
        $groups = @($dependenciesNode)
    }
    foreach ($group in $groups) {
        $dependencies += @($group.dependency | Where-Object { $null -ne $_ })
    }
    return $dependencies
}

function Assert-RequiredDependency(
    [string]$PackageId,
    [object[]]$Dependencies,
    [string]$DependencyId,
    [string]$ExpectedVersion
) {
    $matches = @($Dependencies | Where-Object { $_.id -eq $DependencyId })
    if ($matches.Count -eq 0) {
        throw "Package $PackageId must declare a dependency on $DependencyId."
    }
    foreach ($match in $matches) {
        if ($match.version -ne $ExpectedVersion) {
            throw "Package $PackageId depends on $DependencyId version '$($match.version)'; expected '$ExpectedVersion'."
        }
    }
}

# --- Generate the three same-version packages -------------------------------

$projects = @(
    "src\Rhombus.WinFormsMcp.Server\Rhombus.WinFormsMcp.Server.csproj",
    "src\Rhombus.WinFormsMcp.RuntimeContracts\Rhombus.WinFormsMcp.RuntimeContracts.csproj",
    "src\Rhombus.WinFormsMcp.RuntimeBridge\Rhombus.WinFormsMcp.RuntimeBridge.csproj"
)

Push-Location $repoRoot
try {
    foreach ($project in $projects) {
        Invoke-Dotnet @(
            "pack", $project,
            "--configuration", $Configuration, "--no-restore",
            "/p:Version=$Version", "/p:PackageOutputPath=$nugetRoot"
        )
    }
}
finally {
    Pop-Location
}

$expectedPackages = @(
    "Rhombus.WinFormsMcp.$Version.nupkg",
    "Rhombus.WinFormsMcp.RuntimeContracts.$Version.nupkg",
    "Rhombus.WinFormsMcp.RuntimeBridge.$Version.nupkg"
)
foreach ($packageName in $expectedPackages) {
    $packagePath = Join-Path $nugetRoot $packageName
    if (-not (Test-Path $packagePath)) {
        throw "Expected NuGet package was not created: $packageName"
    }
}

# --- Content checks ----------------------------------------------------------

$serverPackage = Join-Path $nugetRoot "Rhombus.WinFormsMcp.$Version.nupkg"
$contractsPackage = Join-Path $nugetRoot "Rhombus.WinFormsMcp.RuntimeContracts.$Version.nupkg"
$bridgePackage = Join-Path $nugetRoot "Rhombus.WinFormsMcp.RuntimeBridge.$Version.nupkg"

$serverNuspec = Get-Nuspec $serverPackage
$contractsNuspec = Get-Nuspec $contractsPackage
$bridgeNuspec = Get-Nuspec $bridgePackage

foreach ($nuspec in @($serverNuspec, $contractsNuspec, $bridgeNuspec)) {
    $packageId = $nuspec.package.metadata.id
    $packageVersion = $nuspec.package.metadata.version
    if ($packageVersion -ne $Version) {
        throw "Package $packageId has version '$packageVersion'; expected '$Version'."
    }
}

# RuntimeContracts is intentionally a single-target netstandard2.0 assembly so
# that .NET Framework 4.7.2/4.8 and .NET 8 consumers share one contracts DLL.
$contractsEntries = Get-ZipEntryNames $contractsPackage
$contractsLib = @($contractsEntries | Where-Object { $_ -like "lib/*" })
if ($contractsLib.Count -ne 1 -or $contractsLib[0] -ne "lib/netstandard2.0/Rhombus.WinFormsMcp.RuntimeContracts.dll") {
    throw "RuntimeContracts package must contain exactly the netstandard2.0 asset; found: $($contractsLib -join ', ')"
}

# RuntimeBridge must ship the .NET Framework 4.7.2 target plus the 4.8 and the
# SDK-normalized net8.0-windows (net8.0-windows7.0) targets.
$bridgeEntries = Get-ZipEntryNames $bridgePackage
foreach ($asset in @(
    "lib/net472/Rhombus.WinFormsMcp.RuntimeBridge.dll",
    "lib/net48/Rhombus.WinFormsMcp.RuntimeBridge.dll",
    "lib/net8.0-windows7.0/Rhombus.WinFormsMcp.RuntimeBridge.dll"
)) {
    if ($bridgeEntries -notcontains $asset) {
        throw "RuntimeBridge package is missing the $asset asset. Found: $(($bridgeEntries | Where-Object { $_ -like 'lib/*' }) -join ', ')"
    }
}

$serverLib = @(Get-ZipEntryNames $serverPackage | Where-Object { $_ -like "lib/*" })
if (-not ($serverLib | Where-Object { $_ -like "lib/net8.0-windows7.0/*" })) {
    throw "Server package must contain a net8.0-windows (net8.0-windows7.0) asset. Found: $($serverLib -join ', ')"
}
# Rendering is not a publishable package; the Server package must embed it so
# the three-package dependency closure stays closed.
if ($serverLib -notcontains "lib/net8.0-windows7.0/Rhombus.WinFormsMcp.Rendering.dll") {
    throw "Server package must embed Rhombus.WinFormsMcp.Rendering.dll to keep the package closure closed. Found: $($serverLib -join ', ')"
}

# Inter-project dependencies must exist and close on the exact same version.
$serverDependencies = @(Get-NuspecDependencies $serverNuspec)
$bridgeDependencies = @(Get-NuspecDependencies $bridgeNuspec)
Assert-RequiredDependency "Rhombus.WinFormsMcp" $serverDependencies `
    "Rhombus.WinFormsMcp.RuntimeContracts" $Version
Assert-RequiredDependency "Rhombus.WinFormsMcp.RuntimeBridge" $bridgeDependencies `
    "Rhombus.WinFormsMcp.RuntimeContracts" $Version

if (@($serverDependencies | Where-Object { $_.id -eq "Rhombus.WinFormsMcp.Rendering" }).Count -ne 0) {
    throw "Server package must embed Rendering.dll and must not depend on the unpublished Rhombus.WinFormsMcp.Rendering package."
}

foreach ($package in @(
    [pscustomobject]@{ Id = "Rhombus.WinFormsMcp"; Dependencies = $serverDependencies },
    [pscustomobject]@{ Id = "Rhombus.WinFormsMcp.RuntimeBridge"; Dependencies = $bridgeDependencies }
)) {
    $packageId = $package.Id
    $dependencies = $package.Dependencies
    foreach ($dependency in $dependencies) {
        if ($dependency.id -like "Rhombus.WinFormsMcp.*" -and $dependency.version -ne $Version) {
            throw "Package $packageId depends on $($dependency.id) version '$($dependency.version)'; expected '$Version'."
        }
    }
}

Write-Host "NuGet package checks passed for version $Version."
Write-Host "Packages: $nugetRoot"
