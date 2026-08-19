param(
    [string]$Configuration = "Release",
    [string]$Version = "",
    [string]$OutputRoot = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Content (Join-Path $repoRoot "VERSION") -Raw).Trim()
}
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path ([IO.Path]::GetTempPath()) "winforms-mcp-package-$([Guid]::NewGuid().ToString('N'))"
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$stagingRoot = Join-Path $OutputRoot "staging"
$serverOutput = Join-Path $repoRoot "src\Rhombus.WinFormsMcp.Server\bin\$Configuration\net8.0-windows"
$rendererOutput = Join-Path $repoRoot "src\Rhombus.WinFormsMcp.RendererHost\bin\$Configuration"
$serverPackage = Join-Path $OutputRoot "nuget"
$npmPackage = Join-Path $OutputRoot "npm"
$distribution = Join-Path $OutputRoot "winformsmcp-v$Version-win-x64.zip"

function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw "npm is required for the local NPM package check."
}

New-Item -ItemType Directory -Path $OutputRoot, $stagingRoot, $serverPackage, $npmPackage -Force | Out-Null

Push-Location $repoRoot
try {
    Invoke-Dotnet @(
        "pack", "src\Rhombus.WinFormsMcp.Server\Rhombus.WinFormsMcp.Server.csproj",
        "--configuration", $Configuration, "--no-restore", "/p:Version=$Version",
        "/p:PackageOutputPath=$serverPackage"
    )
    Invoke-Dotnet @(
        "pack", "src\Rhombus.WinFormsMcp.RuntimeContracts\Rhombus.WinFormsMcp.RuntimeContracts.csproj",
        "--configuration", $Configuration, "--no-restore", "/p:Version=$Version",
        "/p:PackageOutputPath=$serverPackage"
    )
    Invoke-Dotnet @(
        "pack", "src\Rhombus.WinFormsMcp.RuntimeBridge\Rhombus.WinFormsMcp.RuntimeBridge.csproj",
        "--configuration", $Configuration, "--no-restore", "/p:Version=$Version",
        "/p:PackageOutputPath=$serverPackage"
    )

    $expectedPackages = @(
        "Rhombus.WinFormsMcp.$Version.nupkg",
        "Rhombus.WinFormsMcp.RuntimeContracts.$Version.nupkg",
        "Rhombus.WinFormsMcp.RuntimeBridge.$Version.nupkg"
    )
    foreach ($packageName in $expectedPackages) {
        if (-not (Test-Path (Join-Path $serverPackage $packageName))) {
            throw "Expected NuGet package was not created: $packageName"
        }
    }

    if (-not (Test-Path (Join-Path $serverOutput "winformsmcp.exe"))) {
        throw "Server output was not found at $serverOutput. Build the solution before packaging."
    }
    Copy-Item (Join-Path $serverOutput "*") $stagingRoot -Recurse -Force

    foreach ($tfm in @("net48", "netcoreapp3.1", "net8.0-windows")) {
        $source = Join-Path $rendererOutput $tfm
        if (-not (Test-Path $source)) {
            throw "RendererHost output for $tfm was not found at $source."
        }
        $destination = Join-Path $stagingRoot "rendererhost\$tfm"
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        Copy-Item (Join-Path $source "*") $destination -Recurse -Force
    }

    Compress-Archive -Path (Join-Path $stagingRoot "*") -DestinationPath $distribution -Force
    if (-not (Test-Path $distribution)) {
        throw "Standalone distribution was not created: $distribution"
    }
    $archive = [System.IO.Compression.ZipFile]::OpenRead($distribution)
    try {
        $archiveEntries = @($archive.Entries | ForEach-Object FullName)
        if ($archiveEntries -notcontains "winformsmcp.exe") {
            throw "Standalone distribution is missing winformsmcp.exe."
        }
        foreach ($tfm in @("net48", "netcoreapp3.1", "net8.0-windows")) {
            if (-not ($archiveEntries | Where-Object { $_.StartsWith("rendererhost/$tfm/", [System.StringComparison]::OrdinalIgnoreCase) })) {
                throw "Standalone distribution is missing RendererHost output for $tfm."
            }
        }
    }
    finally {
        $archive.Dispose()
    }

    $npmSource = Join-Path $repoRoot "npm"
    $npmStaging = Join-Path $OutputRoot "npm-staging"
    New-Item -ItemType Directory -Path $npmStaging -Force | Out-Null
    Copy-Item (Join-Path $npmSource "*") $npmStaging -Recurse -Force
    $packageJsonPath = Join-Path $npmStaging "package.json"
    $packageJson = Get-Content $packageJsonPath -Raw | ConvertFrom-Json
    $packageJson.version = $Version
    $packageJson | ConvertTo-Json -Depth 10 | Set-Content $packageJsonPath -Encoding utf8
    New-Item -ItemType Directory -Path (Join-Path $npmStaging "dist") -Force | Out-Null
    Copy-Item (Join-Path $stagingRoot "*") (Join-Path $npmStaging "dist") -Recurse -Force
    Push-Location $npmStaging
    try {
        & npm pack --pack-destination $npmPackage
        if ($LASTEXITCODE -ne 0) {
            throw "npm pack failed with exit code $LASTEXITCODE."
        }
        $npmTarballs = @(Get-ChildItem -Path $npmPackage -Filter "*.tgz" -File)
        if ($npmTarballs.Count -ne 1) {
            throw "Expected exactly one NPM tarball, found $($npmTarballs.Count)."
        }
    }
    finally {
        Pop-Location
    }
}
finally {
    Pop-Location
}

Write-Host "Local package preparation completed without publishing."
Write-Host "Output: $OutputRoot"
Write-Host "NuGet packages: $serverPackage"
Write-Host "NPM package: $npmPackage"
Write-Host "Standalone ZIP: $distribution"
