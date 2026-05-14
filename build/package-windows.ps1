param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "artifacts",
    [string]$PackageVersion = "",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($PackageVersion)) {
    $PackageVersion = Get-Date -Format "yyyyMMdd-HHmmss"
}

$packageName = "memora-$PackageVersion-$Runtime"
$artifactsRoot = Join-Path $repoRoot $OutputDir
$stagingRoot = Join-Path $artifactsRoot $packageName
$publishRoot = Join-Path $stagingRoot "app"
$scriptsRoot = Join-Path $stagingRoot "scripts"
$uiPublish = Join-Path $publishRoot "Memora.Ui"
$apiPublish = Join-Path $publishRoot "Memora.Api"
$zipPath = Join-Path $artifactsRoot "$packageName.zip"

function Write-Utf8NoBomFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Content
    )

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Force -Path $uiPublish, $apiPublish, $scriptsRoot | Out-Null

if (-not $SkipBuild) {
    dotnet build (Join-Path $repoRoot "Memora.sln") -c $Configuration
}

dotnet publish (Join-Path $repoRoot "src/Memora.Ui/Memora.Ui.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $uiPublish

dotnet publish (Join-Path $repoRoot "src/Memora.Api/Memora.Api.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $apiPublish

@"
# Memora Windows Package

This package contains self-contained Windows builds of:

- Memora.Ui
- Memora.Api

## Install

From this folder:

```powershell
.\install-memora.ps1
```

The installer copies the package to `%LOCALAPPDATA%\Memora`, creates a default
workspace root at `%USERPROFILE%\memora-workspaces`, and adds Start Menu
shortcuts for the local UI and API.

## Run Without Installing

```powershell
.\scripts\Start-Memora.ps1
```

By default, Memora uses `%USERPROFILE%\memora-workspaces` unless
`MEMORA_WORKSPACES_ROOT` is already set.

## Local URLs

- UI: http://127.0.0.1:5080
- API: http://127.0.0.1:5081
- OpenAPI: http://127.0.0.1:5081/openapi.json
"@ | ForEach-Object { Write-Utf8NoBomFile -Path (Join-Path $stagingRoot "README.md") -Content $_ }

@"
param(
    [string]`$InstallRoot = "`$env:LOCALAPPDATA\Memora",
    [string]`$WorkspacesRoot = "`$env:USERPROFILE\memora-workspaces"
)

`$ErrorActionPreference = "Stop"

`$sourceRoot = Split-Path -Parent `$MyInvocation.MyCommand.Path
`$installRootFull = [System.IO.Path]::GetFullPath(`$InstallRoot)
`$workspacesRootFull = [System.IO.Path]::GetFullPath(`$WorkspacesRoot)

New-Item -ItemType Directory -Force -Path `$installRootFull, `$workspacesRootFull | Out-Null
`$tokenDirectory = Join-Path `$workspacesRootFull ".memora"
`$tokenPath = Join-Path `$tokenDirectory "local-access-token"
if (-not (Test-Path -LiteralPath `$tokenPath)) {
    New-Item -ItemType Directory -Force -Path `$tokenDirectory | Out-Null
    `$bytes = New-Object byte[] 32
    `$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        `$rng.GetBytes(`$bytes)
    }
    finally {
        `$rng.Dispose()
    }

    `$token = [System.BitConverter]::ToString(`$bytes).Replace("-", "")
    `$encoding = New-Object System.Text.UTF8Encoding(`$false)
    [System.IO.File]::WriteAllText(`$tokenPath, "`$token`n", `$encoding)
}

Get-ChildItem -LiteralPath `$sourceRoot -Force |
    Where-Object { `$_.Name -notin @("install-memora.ps1") } |
    Copy-Item -Destination `$installRootFull -Recurse -Force

`$startMenu = Join-Path `$env:APPDATA "Microsoft\Windows\Start Menu\Programs\Memora"
New-Item -ItemType Directory -Force -Path `$startMenu | Out-Null

`$shell = New-Object -ComObject WScript.Shell

`$uiShortcut = `$shell.CreateShortcut((Join-Path `$startMenu "Memora UI.lnk"))
`$uiShortcut.TargetPath = "powershell.exe"
`$uiShortcut.Arguments = '-ExecutionPolicy Bypass -NoExit -File "{0}" -WorkspacesRoot "{1}"' -f (Join-Path `$installRootFull "scripts\Start-MemoraUi.ps1"), `$workspacesRootFull
`$uiShortcut.WorkingDirectory = `$installRootFull
`$uiShortcut.Save()

`$apiShortcut = `$shell.CreateShortcut((Join-Path `$startMenu "Memora API.lnk"))
`$apiShortcut.TargetPath = "powershell.exe"
`$apiShortcut.Arguments = '-ExecutionPolicy Bypass -NoExit -File "{0}" -WorkspacesRoot "{1}"' -f (Join-Path `$installRootFull "scripts\Start-MemoraApi.ps1"), `$workspacesRootFull
`$apiShortcut.WorkingDirectory = `$installRootFull
`$apiShortcut.Save()

`$bothShortcut = `$shell.CreateShortcut((Join-Path `$startMenu "Start Memora.lnk"))
`$bothShortcut.TargetPath = "powershell.exe"
`$bothShortcut.Arguments = '-ExecutionPolicy Bypass -NoExit -File "{0}" -WorkspacesRoot "{1}"' -f (Join-Path `$installRootFull "scripts\Start-Memora.ps1"), `$workspacesRootFull
`$bothShortcut.WorkingDirectory = `$installRootFull
`$bothShortcut.Save()

Write-Host "Memora installed to: `$installRootFull"
Write-Host "Workspace root: `$workspacesRootFull"
Write-Host "Start Menu shortcuts: `$startMenu"
Write-Host ""
Write-Host "Run 'Start Memora' from the Start Menu, then open http://127.0.0.1:5080"
Write-Host "To set the local browser session, open http://127.0.0.1:5080/?localToken=`$((Get-Content -Raw -LiteralPath `$tokenPath).Trim())"
"@ | ForEach-Object { Write-Utf8NoBomFile -Path (Join-Path $stagingRoot "install-memora.ps1") -Content $_ }

@"
param(
    [string]`$InstallRoot = "`$env:LOCALAPPDATA\Memora"
)

`$ErrorActionPreference = "Stop"

`$startMenu = Join-Path `$env:APPDATA "Microsoft\Windows\Start Menu\Programs\Memora"
if (Test-Path -LiteralPath `$startMenu) {
    Remove-Item -LiteralPath `$startMenu -Recurse -Force
}

if (Test-Path -LiteralPath `$InstallRoot) {
    Remove-Item -LiteralPath `$InstallRoot -Recurse -Force
}

Write-Host "Memora application files and Start Menu shortcuts were removed."
Write-Host "Workspace files were left in place."
"@ | ForEach-Object { Write-Utf8NoBomFile -Path (Join-Path $stagingRoot "uninstall-memora.ps1") -Content $_ }

@"
param(
    [string]`$WorkspacesRoot = "`$env:USERPROFILE\memora-workspaces"
)

`$ErrorActionPreference = "Stop"

`$scriptRoot = Split-Path -Parent `$MyInvocation.MyCommand.Path
`$packageRoot = Split-Path -Parent `$scriptRoot
`$env:MEMORA_WORKSPACES_ROOT = [System.IO.Path]::GetFullPath(`$WorkspacesRoot)
New-Item -ItemType Directory -Force -Path `$env:MEMORA_WORKSPACES_ROOT | Out-Null
`$tokenDirectory = Join-Path `$env:MEMORA_WORKSPACES_ROOT ".memora"
`$tokenPath = Join-Path `$tokenDirectory "local-access-token"
if (-not (Test-Path -LiteralPath `$tokenPath)) {
    New-Item -ItemType Directory -Force -Path `$tokenDirectory | Out-Null
    `$bytes = New-Object byte[] 32
    `$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { `$rng.GetBytes(`$bytes) } finally { `$rng.Dispose() }
    `$token = [System.BitConverter]::ToString(`$bytes).Replace("-", "")
    `$encoding = New-Object System.Text.UTF8Encoding(`$false)
    [System.IO.File]::WriteAllText(`$tokenPath, "`$token`n", `$encoding)
}

& (Join-Path `$packageRoot "app\Memora.Ui\Memora.Ui.exe")
"@ | ForEach-Object { Write-Utf8NoBomFile -Path (Join-Path $scriptsRoot "Start-MemoraUi.ps1") -Content $_ }

@"
param(
    [string]`$WorkspacesRoot = "`$env:USERPROFILE\memora-workspaces"
)

`$ErrorActionPreference = "Stop"

`$scriptRoot = Split-Path -Parent `$MyInvocation.MyCommand.Path
`$packageRoot = Split-Path -Parent `$scriptRoot
`$env:MEMORA_WORKSPACES_ROOT = [System.IO.Path]::GetFullPath(`$WorkspacesRoot)
New-Item -ItemType Directory -Force -Path `$env:MEMORA_WORKSPACES_ROOT | Out-Null
`$tokenDirectory = Join-Path `$env:MEMORA_WORKSPACES_ROOT ".memora"
`$tokenPath = Join-Path `$tokenDirectory "local-access-token"
if (-not (Test-Path -LiteralPath `$tokenPath)) {
    New-Item -ItemType Directory -Force -Path `$tokenDirectory | Out-Null
    `$bytes = New-Object byte[] 32
    `$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { `$rng.GetBytes(`$bytes) } finally { `$rng.Dispose() }
    `$token = [System.BitConverter]::ToString(`$bytes).Replace("-", "")
    `$encoding = New-Object System.Text.UTF8Encoding(`$false)
    [System.IO.File]::WriteAllText(`$tokenPath, "`$token`n", `$encoding)
}

& (Join-Path `$packageRoot "app\Memora.Api\Memora.Api.exe")
"@ | ForEach-Object { Write-Utf8NoBomFile -Path (Join-Path $scriptsRoot "Start-MemoraApi.ps1") -Content $_ }

@"
param(
    [string]`$WorkspacesRoot = "`$env:USERPROFILE\memora-workspaces"
)

`$ErrorActionPreference = "Stop"

`$scriptRoot = Split-Path -Parent `$MyInvocation.MyCommand.Path
`$packageRoot = Split-Path -Parent `$scriptRoot
`$workspacesRootFull = [System.IO.Path]::GetFullPath(`$WorkspacesRoot)
New-Item -ItemType Directory -Force -Path `$workspacesRootFull | Out-Null
`$tokenDirectory = Join-Path `$workspacesRootFull ".memora"
`$tokenPath = Join-Path `$tokenDirectory "local-access-token"
if (-not (Test-Path -LiteralPath `$tokenPath)) {
    New-Item -ItemType Directory -Force -Path `$tokenDirectory | Out-Null
    `$bytes = New-Object byte[] 32
    `$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { `$rng.GetBytes(`$bytes) } finally { `$rng.Dispose() }
    `$token = [System.BitConverter]::ToString(`$bytes).Replace("-", "")
    `$encoding = New-Object System.Text.UTF8Encoding(`$false)
    [System.IO.File]::WriteAllText(`$tokenPath, "`$token`n", `$encoding)
}

`$apiScript = Join-Path `$scriptRoot "Start-MemoraApi.ps1"
`$uiScript = Join-Path `$scriptRoot "Start-MemoraUi.ps1"

`$apiArguments = '-ExecutionPolicy Bypass -NoExit -File "{0}" -WorkspacesRoot "{1}"' -f `$apiScript, `$workspacesRootFull
Start-Process powershell.exe -WorkingDirectory `$packageRoot -ArgumentList `$apiArguments

Start-Sleep -Seconds 2

`$uiArguments = '-ExecutionPolicy Bypass -NoExit -File "{0}" -WorkspacesRoot "{1}"' -f `$uiScript, `$workspacesRootFull
Start-Process powershell.exe -WorkingDirectory `$packageRoot -ArgumentList `$uiArguments

Write-Host "Memora API: http://127.0.0.1:5081"
Write-Host "Memora UI:  http://127.0.0.1:5080"
`$token = (Get-Content -Raw -LiteralPath `$tokenPath).Trim([char]0xFEFF).Trim()
Write-Host "Login URL:   http://127.0.0.1:5080/?localToken=`$token"
"@ | ForEach-Object { Write-Utf8NoBomFile -Path (Join-Path $scriptsRoot "Start-Memora.ps1") -Content $_ }

$itemsToArchive = Get-ChildItem -LiteralPath $stagingRoot -Force
Compress-Archive -LiteralPath $itemsToArchive.FullName -DestinationPath $zipPath -Force

Write-Host "Package folder: $stagingRoot"
Write-Host "Package zip:    $zipPath"
