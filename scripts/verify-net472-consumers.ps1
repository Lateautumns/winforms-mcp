param(
    [string]$Configuration = "Release",
    [string]$PackageRoot = ""
)

# End-to-end verification of the .NET Framework 4.7.2 RuntimeBridge support:
#   1. Uses a supplied package directory, or packs the three same-version NuGet
#      packages into a unique temp directory when -PackageRoot is omitted.
#   2. Generates a NuGet.config with package source mapping so
#      Rhombus.WinFormsMcp.* resolves only from the local source and every
#      other dependency resolves from nuget.org.
#   3. Restores and builds the SDK-style and the traditional (non-SDK) net472
#      consumer projects with Visual Studio MSBuild.
#   4. Checks the consumer output configs for the .NET Framework 4.7.2
#      supportedRuntime entry and auto-generated binding redirects.
#   5. Launches each consumer, connects to winforms-mcp-runtime-<pid>, and
#      verifies hello (Protocol v1, .NET Framework runtime, bridge version,
#      non-empty instance id, uiThreadSnapshots capability) plus
#      get_control_tree (expected Form and Button).
#   6. Shuts the consumer down gracefully; on timeout or failure the test
#      process is killed and the temp directory is removed.

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$version = (Get-Content (Join-Path $repoRoot "VERSION") -Raw).Trim()
$sourceConsumersRoot = Join-Path $repoRoot "tests\consumers"
$workRoot = Join-Path ([IO.Path]::GetTempPath()) "winforms-mcp-net472-$([Guid]::NewGuid().ToString('N'))"
$consumersRoot = Join-Path $workRoot "consumers"
$consumerProjects = @(
    (Join-Path $consumersRoot "Net472Consumer.Sdk\Net472Consumer.Sdk.csproj"),
    (Join-Path $consumersRoot "Net472Consumer.Legacy\Net472Consumer.Legacy.csproj")
)
$packagesRoot = Join-Path $workRoot "packages"

$consumerProcesses = @()

function Find-MSBuild {
    if ($env:MSBUILD_PATH -and (Test-Path $env:MSBUILD_PATH)) {
        return $env:MSBUILD_PATH
    }
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" 2>$null |
            Where-Object { $_ } | Select-Object -First 1
        if ($found) {
            return $found
        }
    }
    $candidates = @(
        "C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
    )
    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }
    throw "Visual Studio MSBuild was not found. Install Visual Studio with the .NET desktop workload or set MSBUILD_PATH."
}

function Assert-Condition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Invoke-BridgeRequest {
    param(
        [string]$PipeName,
        [string]$Command,
        [int]$ProcessId,
        [string]$BridgeInstanceId = ""
    )
    $client = New-Object System.IO.Pipes.NamedPipeClientStream(
        ".",
        $PipeName,
        [System.IO.Pipes.PipeDirection]::InOut,
        [System.IO.Pipes.PipeOptions]::Asynchronous)
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds(30)
        $connected = $false
        while (-not $connected) {
            if ([DateTime]::UtcNow -gt $deadline) {
                throw "Timed out waiting for the bridge pipe '$PipeName'."
            }
            try {
                $client.Connect(1000)
                $connected = $true
            }
            catch {
                Start-Sleep -Milliseconds 200
            }
        }

        $requestBody = [ordered]@{
            protocolVersion  = 1
            requestId        = [Guid]::NewGuid().ToString("N")
            command          = $Command
            pid              = $ProcessId
            bridgeInstanceId = $BridgeInstanceId
            arguments        = [ordered]@{}
        }
        $requestJson = $requestBody | ConvertTo-Json -Depth 5 -Compress

        $writer = New-Object System.IO.StreamWriter($client, (New-Object System.Text.UTF8Encoding($false)), 4096)
        $writer.AutoFlush = $true
        $writer.WriteLine($requestJson)

        $reader = New-Object System.IO.StreamReader($client, (New-Object System.Text.UTF8Encoding($false)), $true, 4096, $true)
        $readTask = $reader.ReadLineAsync()
        $timeoutTask = [System.Threading.Tasks.Task]::Delay([TimeSpan]::FromSeconds(30))
        $completedTask = [System.Threading.Tasks.Task]::WhenAny(
            [System.Threading.Tasks.Task[]]@($readTask, $timeoutTask)).GetAwaiter().GetResult()
        if (-not [object]::ReferenceEquals($completedTask, $readTask)) {
            throw "Timed out waiting for the bridge response to '$Command'."
        }
        $line = $readTask.GetAwaiter().GetResult()
        if ($null -eq $line) {
            throw "The bridge closed the pipe before responding to '$Command'."
        }
        return ($line | ConvertFrom-Json)
    }
    finally {
        $client.Dispose()
    }
}

function Stop-ConsumerProcess {
    param($Process)
    if ($Process.HasExited) {
        return
    }
    $null = $Process.CloseMainWindow()
    if (-not $Process.WaitForExit(10000)) {
        $Process.Kill()
        $null = $Process.WaitForExit(5000)
        Write-Warning "Consumer process $($Process.Id) was killed after the graceful close timeout."
    }
}

function Find-ControlName {
    param($Node, [string]$Name)
    if ($null -eq $Node) {
        return $false
    }
    if ($Node.summary -and $Node.summary.identity -and $Node.summary.identity.name -eq $Name) {
        return $true
    }
    if ($Node.roots) {
        foreach ($root in $Node.roots) {
            if (Find-ControlName $root $Name) {
                return $true
            }
        }
    }
    if ($Node.children) {
        foreach ($child in $Node.children) {
            if (Find-ControlName $child $Name) {
                return $true
            }
        }
    }
    return $false
}

try {
    New-Item -ItemType Directory -Path $consumersRoot -Force | Out-Null
    foreach ($directoryName in @("Net472Consumer.Sdk", "Net472Consumer.Legacy", "Shared")) {
        $sourceDirectory = Join-Path $sourceConsumersRoot $directoryName
        $destinationDirectory = Join-Path $consumersRoot $directoryName
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
        Get-ChildItem -LiteralPath $sourceDirectory -File |
            Copy-Item -Destination $destinationDirectory -Force
    }
    New-Item -ItemType Directory -Path $packagesRoot -Force | Out-Null

    # 1. Use the already gated packages when supplied; otherwise pack locally.
    if ([string]::IsNullOrWhiteSpace($PackageRoot)) {
        & (Join-Path $PSScriptRoot "pack-nuget.ps1") -Configuration $Configuration -Version $version -OutputRoot $workRoot
        $nugetRoot = Join-Path $workRoot "nuget"
    }
    else {
        $nugetRoot = (Resolve-Path $PackageRoot).Path
    }
    foreach ($packageName in @(
        "Rhombus.WinFormsMcp.$version.nupkg",
        "Rhombus.WinFormsMcp.RuntimeContracts.$version.nupkg",
        "Rhombus.WinFormsMcp.RuntimeBridge.$version.nupkg"
    )) {
        if (-not (Test-Path (Join-Path $nugetRoot $packageName))) {
            throw "Package root '$nugetRoot' does not contain the expected package '$packageName'."
        }
    }

    # 2. NuGet.config with package source mapping.
    $nugetConfig = Join-Path $workRoot "NuGet.config"
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$nugetRoot" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="local">
      <package pattern="Rhombus.WinFormsMcp*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"@ | Set-Content -Path $nugetConfig -Encoding utf8

    # 3. Restore and build both consumers with Visual Studio MSBuild.
    $msbuild = Find-MSBuild
    Write-Host "Using MSBuild: $msbuild"
    foreach ($project in $consumerProjects) {
        & $msbuild $project "/t:Restore,Rebuild" "/p:Configuration=$Configuration" `
            "/p:RuntimeBridgePackageVersion=$version" "/p:RestoreConfigFile=$nugetConfig" `
            "/p:RestorePackagesPath=$packagesRoot" "/p:RestoreNoCache=true" "/p:RestoreForce=true" `
            "/v:minimal" "/nologo"
        if ($LASTEXITCODE -ne 0) {
            throw "MSBuild failed for $project (exit code $LASTEXITCODE)."
        }
    }

    # 4. Config checks + 5. runtime E2E for each consumer.
    foreach ($project in $consumerProjects) {
        $consumerName = [System.IO.Path]::GetFileNameWithoutExtension($project)
        $binDir = Join-Path (Split-Path $project) "bin\$Configuration"
        # SDK-style projects place the output in a TFM subfolder (bin\Release\net472),
        # while legacy projects place it directly in bin\Release.
        $exe = Get-ChildItem -Path $binDir -Filter "$consumerName.exe" -File -Recurse | Select-Object -First 1
        if ($null -eq $exe) {
            throw "Consumer output exe was not found under $binDir."
        }
        $configPath = "$($exe.FullName).config"
        if (-not (Test-Path $configPath)) {
            throw "Consumer output config was not found: $configPath"
        }
        $configText = Get-Content $configPath -Raw
        Assert-Condition ($configText -match '\.NETFramework,Version=v4\.7\.2') `
            "Consumer $consumerName output config is missing the .NET Framework 4.7.2 supportedRuntime entry."
        Assert-Condition ($configText -match '<bindingRedirect') `
            "Consumer $consumerName output config is missing auto-generated binding redirects."

        Write-Host "Starting consumer ${consumerName}: $($exe.FullName)"
        $process = Start-Process -FilePath $exe.FullName -PassThru
        $consumerProcesses += $process
        try {
            $pipeName = "winforms-mcp-runtime-$($process.Id)"
            $hello = Invoke-BridgeRequest -PipeName $pipeName -Command "hello" -ProcessId $process.Id
            Assert-Condition $hello.success "Consumer $consumerName hello failed: $($hello.error.message)"
            Assert-Condition ($hello.result.protocolVersion -eq 1) "Consumer $consumerName reported protocol version $($hello.result.protocolVersion); expected 1."
            Assert-Condition ($hello.result.process.runtime -like ".NET Framework*") `
                "Consumer $consumerName runtime is '$($hello.result.process.runtime)'; expected '.NET Framework...' (exact CLR revision must not be pinned)."
            Assert-Condition ($hello.result.process.bridgeVersion -eq $version) `
                "Consumer $consumerName bridge version is '$($hello.result.process.bridgeVersion)'; expected '$version'."
            Assert-Condition (-not [string]::IsNullOrWhiteSpace($hello.result.bridgeInstanceId)) `
                "Consumer $consumerName hello returned an empty bridge instance id."
            Assert-Condition (@($hello.result.capabilities) -contains "uiThreadSnapshots") `
                "Consumer $consumerName hello is missing the uiThreadSnapshots capability."

            $tree = Invoke-BridgeRequest -PipeName $pipeName -Command "get_control_tree" -ProcessId $process.Id -BridgeInstanceId $hello.result.bridgeInstanceId
            Assert-Condition $tree.success "Consumer $consumerName get_control_tree failed: $($tree.error.message)"
            Assert-Condition (Find-ControlName $tree.result "net472ConsumerForm") `
                "Consumer $consumerName control tree does not contain the expected Form 'net472ConsumerForm'."
            Assert-Condition (Find-ControlName $tree.result "verifyButton") `
                "Consumer $consumerName control tree does not contain the expected Button 'verifyButton'."

            Write-Host "Consumer $consumerName verified: Protocol v1, .NET Framework runtime, bridge version $version, uiThreadSnapshots, real control tree."
        }
        finally {
            Stop-ConsumerProcess $process
        }
    }
}
finally {
    foreach ($process in $consumerProcesses) {
        if (-not $process.HasExited) {
            $process.Kill()
            $null = $process.WaitForExit(5000)
        }
    }
    if (Test-Path $workRoot) {
        Remove-Item $workRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "Both .NET Framework 4.7.2 consumers passed the end-to-end RuntimeBridge verification."
