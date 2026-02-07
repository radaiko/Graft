$ErrorActionPreference = 'Stop'

# Detect architecture
$arch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'x64' }
$rid = "win-$arch"

# Get latest CLI version
$releases = Invoke-RestMethod "https://api.github.com/repos/radaiko/Graft/releases"
$latest = ($releases | Where-Object { $_.tag_name -match '^cli/v' } | Select-Object -First 1).tag_name -replace 'cli/v', ''

if (-not $latest) {
    Write-Error "Failed to determine latest version"
    exit 1
}

Write-Host "Installing Graft CLI v$latest ($rid)..."

$url = "https://github.com/radaiko/Graft/releases/download/cli/v$latest/graft-cli-v$latest-$rid.zip"
$tmpDir = Join-Path $env:TEMP "graft-install"
$zipPath = Join-Path $tmpDir "graft.zip"

New-Item -ItemType Directory -Force -Path $tmpDir | Out-Null
Invoke-WebRequest -Uri $url -OutFile $zipPath
Expand-Archive -Path $zipPath -DestinationPath $tmpDir -Force

# Install to user's local app directory
$installDir = Join-Path $env:LOCALAPPDATA "Graft"
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Move-Item -Force (Join-Path $tmpDir "graft.exe") $installDir

# Add to PATH if not already there
$currentPath = [Environment]::GetEnvironmentVariable('PATH', 'User')
if ($currentPath -notlike "*$installDir*") {
    [Environment]::SetEnvironmentVariable('PATH', "$installDir;$currentPath", 'User')
    $env:PATH = "$installDir;$env:PATH"
    Write-Host "Added $installDir to PATH"
}

Remove-Item -Recurse -Force $tmpDir

Write-Host "Graft CLI v$latest installed to $installDir\graft.exe"

# Set up gt shortcut and git alias
& (Join-Path $installDir "graft.exe") install
