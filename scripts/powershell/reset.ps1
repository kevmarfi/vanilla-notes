# Resets Vanilla Notes on this Windows account to the state of a first start: deletes the data
# folder (every profile, note, attachment, and backup) and the machine folder (window position and
# diagnostics log). Nothing deleted can be recovered.
#
# Usage: powershell -ExecutionPolicy Bypass -File scripts\powershell\reset.ps1 [-Yes]
#   -Yes   Deletes without asking for confirmation.
#
# The keys of Windows Hello made for private profiles stay in Windows; a profile made later under
# the same name replaces its key.

[CmdletBinding()]
param(
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'

$appName = 'Vanilla Notes'
$processName = 'VanillaNotes'

function Fail([string]$message) {
    [Console]::Error.WriteLine("reset: $message")
    exit 1
}

if (-not $env:APPDATA -or -not $env:LOCALAPPDATA) {
    Fail 'APPDATA or LOCALAPPDATA is not set.'
}

if (Get-Process -Name $processName -ErrorAction SilentlyContinue) {
    Fail "$appName is running. Close it and run the script again."
}

$targets = @(
    (Join-Path $env:APPDATA $appName),
    (Join-Path $env:LOCALAPPDATA $appName)
) | Where-Object { Test-Path -LiteralPath $_ -PathType Container }

if (-not $targets) {
    Write-Output "Nothing to reset: $appName has no data on this account."
    exit 0
}

Write-Output 'This deletes, with no way back:'
foreach ($folder in $targets) {
    Write-Output "  $folder"
}

if (-not $Yes) {
    $answer = Read-Host "Type 'reset' to continue"
    if ($answer -cne 'reset') {
        Fail 'cancelled; nothing was deleted.'
    }
}

foreach ($folder in $targets) {
    Remove-Item -LiteralPath $folder -Recurse -Force
}

Write-Output "$appName was reset. The next start shows the window of profiles, empty."
