param(
    [string]$ProfilePath,
    [string]$VerifyDirectory,
    [switch]$OverlayOnly
)

$ErrorActionPreference = 'Stop'
$repoDirectory = Split-Path -Parent $PSScriptRoot
$localSdk = Join-Path $env:LOCALAPPDATA 'dotnet\dotnet.exe'
$sdkCommand = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { 'dotnet' }
if (-not (& $sdkCommand --list-sdks | Select-String '^8\.')) {
    throw '.NET 8 SDK is required. The portable build can run without an installed SDK.'
}
$env:DOTNET_ROOT = Split-Path -Parent (Get-Command $sdkCommand).Source
Push-Location $repoDirectory
try {
    & $sdkCommand build 'src\StatsInfo\StatsInfo.csproj' -c Release --verbosity minimal
    if ($LASTEXITCODE -ne 0) { throw 'StatsInfo build failed.' }
    $appExecutable = Join-Path $repoDirectory 'src\StatsInfo\bin\Release\net8.0-windows10.0.19041.0\StatsInfo.exe'
    $appArguments = @()
    if (-not $OverlayOnly) { $appArguments += '--settings-window' }
    if ($ProfilePath) {
        $profileTarget = if ([System.IO.Path]::IsPathRooted($ProfilePath)) { $ProfilePath } else {
            Join-Path $repoDirectory $ProfilePath
        }
        $profileAbsolute = [System.IO.Path]::GetFullPath($profileTarget)
        $appArguments += '--settings "' + $profileAbsolute + '"'
    }
    if ($VerifyDirectory) {
        $verifyTarget = if ([System.IO.Path]::IsPathRooted($VerifyDirectory)) { $VerifyDirectory } else {
            Join-Path $repoDirectory $VerifyDirectory
        }
        $verifyAbsolute = [System.IO.Path]::GetFullPath($verifyTarget)
        $appArguments += '--verify "' + $verifyAbsolute + '"'
    }
    $appProcess = if ($appArguments.Count -gt 0) {
        Start-Process -FilePath $appExecutable -ArgumentList ($appArguments -join ' ') -WindowStyle Hidden -PassThru
    } else { Start-Process -FilePath $appExecutable -WindowStyle Hidden -PassThru }
    Write-Output "StatsInfo started locally (PID $($appProcess.Id)). No commit or push was performed."
}
finally { Pop-Location }
