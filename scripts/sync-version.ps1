param(
    [string]$NewVersion = "",
    [switch]$CheckOnly
)

# Transactionally validates and updates the repository version across every
# source of truth: VERSION, npm/package.json, and the three publishable .csproj
# files (Server, RuntimeContracts, RuntimeBridge). All replacements are staged
# first and each original file is retained for rollback until the whole update
# succeeds. With -CheckOnly (or no -NewVersion), the script only validates that
# all sources already agree.

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

$versionFile = Join-Path $repoRoot "VERSION"
$npmFile = Join-Path $repoRoot "npm\package.json"
$csprojFiles = @(
    (Join-Path $repoRoot "src\Rhombus.WinFormsMcp.Server\Rhombus.WinFormsMcp.Server.csproj"),
    (Join-Path $repoRoot "src\Rhombus.WinFormsMcp.RuntimeContracts\Rhombus.WinFormsMcp.RuntimeContracts.csproj"),
    (Join-Path $repoRoot "src\Rhombus.WinFormsMcp.RuntimeBridge\Rhombus.WinFormsMcp.RuntimeBridge.csproj")
)

function Get-VersionSource {
    param([string]$Path)
    $name = (Resolve-Path $Path).Path.Replace($repoRoot, "").TrimStart("\", "/")
    if ($Path -eq $versionFile) {
        return [pscustomobject]@{ Name = $name; Version = (Get-Content $Path -Raw).Trim() }
    }
    if ($Path -eq $npmFile) {
        $packageJson = Get-Content $Path -Raw | ConvertFrom-Json
        return [pscustomobject]@{ Name = $name; Version = $packageJson.version }
    }
    $xml = [xml](Get-Content $Path)
    $node = $xml.SelectSingleNode("//PropertyGroup/Version")
    if ($null -eq $node) {
        throw "No <Version> element found in $Path"
    }
    return [pscustomobject]@{ Name = $name; Version = $node.InnerText.Trim() }
}

$sources = @()
$sources += Get-VersionSource $versionFile
$sources += Get-VersionSource $npmFile
foreach ($csproj in $csprojFiles) {
    $sources += Get-VersionSource $csproj
}

$uniqueVersions = @($sources.Version | Sort-Object -Unique)
if ($uniqueVersions.Count -ne 1) {
    $summary = ($sources | ForEach-Object { "$($_.Name) = $($_.Version)" }) -join "; "
    if ($CheckOnly -or [string]::IsNullOrWhiteSpace($NewVersion)) {
        throw "Version mismatch: $summary"
    }
    Write-Warning "Repairing version mismatch by synchronizing every source to ${NewVersion}: $summary"
    $currentVersion = "mixed versions"
}
else {
    $currentVersion = $uniqueVersions[0]
    Write-Host "All version sources agree on $currentVersion."
}

if ($CheckOnly -or [string]::IsNullOrWhiteSpace($NewVersion)) {
    return
}

if ($NewVersion -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$') {
    throw "Invalid SemVer version '$NewVersion'."
}
if ($uniqueVersions.Count -eq 1 -and $NewVersion -eq $currentVersion) {
    Write-Host "Version is already $NewVersion; nothing to update."
    return
}

# Validate every file parses before writing anything.
foreach ($csproj in $csprojFiles) {
    $null = [xml](Get-Content $csproj)
}
$npm = Get-Content $npmFile -Raw | ConvertFrom-Json
if ($null -eq $npm.version) {
    throw "npm/package.json has no version field."
}

function Replace-CapturedVersion {
    param(
        [string]$Content,
        [string]$Pattern,
        [string]$Replacement,
        [string]$SourceName
    )

    $matches = @([regex]::Matches($Content, $Pattern))
    if ($matches.Count -ne 1 -or -not $matches[0].Groups["value"].Success) {
        throw "Expected exactly one version value in $SourceName; found $($matches.Count)."
    }
    $value = $matches[0].Groups["value"]
    return $Content.Substring(0, $value.Index) + $Replacement +
        $Content.Substring($value.Index + $value.Length)
}

function Write-StagedUtf8File {
    param(
        [string]$TargetPath,
        [string]$Content,
        [string]$TransactionId
    )

    $originalBytes = [IO.File]::ReadAllBytes($TargetPath)
    $hasBom = $originalBytes.Length -ge 3 -and
        $originalBytes[0] -eq 0xEF -and
        $originalBytes[1] -eq 0xBB -and
        $originalBytes[2] -eq 0xBF
    $encoding = New-Object System.Text.UTF8Encoding($hasBom)
    $stagedPath = "$TargetPath.version-sync-$TransactionId.tmp"
    [IO.File]::WriteAllText($stagedPath, $Content, $encoding)
    return $stagedPath
}

$npmText = Get-Content $npmFile -Raw
$updatedNpmText = Replace-CapturedVersion $npmText `
    '(?m)"version"\s*:\s*"(?<value>[^"]*)"' $NewVersion "npm/package.json"

$updatedCsprojText = [ordered]@{}
foreach ($csproj in $csprojFiles) {
    $relativeName = (Resolve-Path $csproj).Path.Replace($repoRoot, "").TrimStart("\", "/")
    $updatedCsprojText[$csproj] = Replace-CapturedVersion (Get-Content $csproj -Raw) `
        '<Version>\s*(?<value>[^<]+?)\s*</Version>' $NewVersion $relativeName
}

$transactionId = [Guid]::NewGuid().ToString("N")
$stagedFiles = [ordered]@{}
$backupFiles = [ordered]@{}
$replacedFiles = New-Object System.Collections.Generic.List[string]
$preserveBackups = $false
try {
    # Stage every new file before replacing any source of truth.
    $stagedFiles[$versionFile] = Write-StagedUtf8File $versionFile $NewVersion $transactionId
    $stagedFiles[$npmFile] = Write-StagedUtf8File $npmFile $updatedNpmText $transactionId
    foreach ($csproj in $csprojFiles) {
        $stagedFiles[$csproj] = Write-StagedUtf8File $csproj $updatedCsprojText[$csproj] $transactionId
    }

    foreach ($targetPath in $stagedFiles.Keys) {
        $backupPath = "$targetPath.version-sync-$transactionId.bak"
        $backupFiles[$targetPath] = $backupPath
        [IO.File]::Replace($stagedFiles[$targetPath], $targetPath, $backupPath)
        $replacedFiles.Add($targetPath)
    }
}
catch {
    $updateError = $_
    $rollbackErrors = @()
    for ($index = $replacedFiles.Count - 1; $index -ge 0; $index--) {
        $targetPath = $replacedFiles[$index]
        $backupPath = $backupFiles[$targetPath]
        try {
            $discardPath = "$targetPath.version-sync-$transactionId.rollback"
            [IO.File]::Replace($backupPath, $targetPath, $discardPath)
            Remove-Item -LiteralPath $discardPath -Force -ErrorAction SilentlyContinue
        }
        catch {
            $rollbackErrors += "$targetPath ($($_.Exception.Message))"
        }
    }

    if ($rollbackErrors.Count -gt 0) {
        $preserveBackups = $true
        throw "Version update failed: $($updateError.Exception.Message). Rollback also failed for: $($rollbackErrors -join '; '). Backup files were preserved."
    }
    throw $updateError
}
finally {
    foreach ($stagedPath in $stagedFiles.Values) {
        if (Test-Path $stagedPath) {
            Remove-Item -LiteralPath $stagedPath -Force -ErrorAction SilentlyContinue
        }
    }
    if (-not $preserveBackups) {
        foreach ($backupPath in $backupFiles.Values) {
            if (Test-Path $backupPath) {
                Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

Write-Host "Version updated from $currentVersion to $NewVersion."
