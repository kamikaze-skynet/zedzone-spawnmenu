# Builds SpawnMenu and uploads it to the Steam Workshop with SteamCMD.
#
#   .\publish-workshop.ps1 -SteamUser <login>                     first upload: creates a Private item
#   .\publish-workshop.ps1 -SteamUser <login> -Visibility Public  make it public
#   .\publish-workshop.ps1 -SteamUser <login> -ChangeNote "Fixed X"
#
# SteamCMD asks for your password / Steam Guard code itself. This script never sees or saves them.
# The first upload writes the new item id to workshop\publishedfileid.txt; commit that file so
# later runs update the same item instead of creating a new one.
param(
    [Parameter(Mandatory = $true)][string]$SteamUser,
    [string]$GameDir = "D:\SteamLibrary\steamapps\common\ZED ZONE",
    [ValidateSet("Public", "FriendsOnly", "Private", "Unlisted")][string]$Visibility = "Private",
    [string]$ChangeNote = "",
    [switch]$UpdateDescription,
    [switch]$SkipBuild
)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$AppId   = "1211600"
$Title   = "Spawn Menu - item spawner and kits for testing"
$Content = Join-Path $PSScriptRoot "workshop\content"
$ModDir  = Join-Path $Content "SpawnMenu"
$Preview = Join-Path $PSScriptRoot "workshop\preview.png"
$IdFile  = Join-Path $PSScriptRoot "workshop\publishedfileid.txt"
$Dll     = Join-Path $PSScriptRoot "src\bin\Release\net6.0\SpawnMenu.dll"

# ---- 1. build and stage the item folder
if (-not $SkipBuild) {
    dotnet build src\SpawnMenu.csproj -c Release "-p:GameDir=$GameDir"
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
}
if (-not (Test-Path $Dll)) { throw "Missing $Dll - build first" }
$Version = [System.Reflection.AssemblyName]::GetAssemblyName($Dll).Version.ToString(3)
Copy-Item $Dll (Join-Path $ModDir "SpawnMenu.dll") -Force
Copy-Item (Join-Path $PSScriptRoot "README.md") (Join-Path $ModDir "README.md") -Force
$modJson = Join-Path $ModDir "mod.json"
$json = Get-Content $modJson -Raw
$json = $json -replace '"version"\s*:\s*"[^"]*"', ('"version": "' + $Version + '"')
[System.IO.File]::WriteAllText($modJson, $json, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "==> Staged SpawnMenu $Version in workshop\content\SpawnMenu" -ForegroundColor Cyan

# ---- 2. SteamCMD (downloaded once into tools\steamcmd)
$Tools = Join-Path $PSScriptRoot "tools\steamcmd"
$SteamCmd = Join-Path $Tools "steamcmd.exe"
if (-not (Test-Path $SteamCmd)) {
    Write-Host "==> Downloading SteamCMD" -ForegroundColor Cyan
    New-Item -ItemType Directory -Force $Tools | Out-Null
    $zip = Join-Path $Tools "steamcmd.zip"
    Invoke-WebRequest "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip" -OutFile $zip -UseBasicParsing
    Expand-Archive -LiteralPath $zip -DestinationPath $Tools -Force
    Remove-Item $zip
}

# ---- 3. item description (VDF)
$Id = "0"
if (Test-Path $IdFile) { $Id = (Get-Content $IdFile -Raw).Trim() }
$isNew = ($Id -eq "0" -or $Id -eq "")
if (-not $ChangeNote) { $ChangeNote = "SpawnMenu $Version" }

function Clean([string]$s) { return ($s -replace '"', "'" -replace '\\', '/') }
function P([string]$p) { return ($p -replace '\\', '/') }

$lines = @(
    '"workshopitem"', '{',
    ('  "appid" "' + $AppId + '"'),
    ('  "publishedfileid" "' + $Id + '"'),
    ('  "contentfolder" "' + (P $Content) + '"'),
    ('  "previewfile" "' + (P $Preview) + '"'),
    ('  "changenote" "' + (Clean $ChangeNote) + '"')
)
if ($isNew -or $PSBoundParameters.ContainsKey("Visibility")) {
    $vis = @{ Public = 0; FriendsOnly = 1; Private = 2; Unlisted = 3 }[$Visibility]
    $lines += ('  "visibility" "' + $vis + '"')
}
if ($isNew -or $UpdateDescription) {
    $desc = Get-Content (Join-Path $PSScriptRoot "workshop\description.txt") -Raw
    $lines += ('  "title" "' + (Clean $Title) + '"')
    $lines += ('  "description" "' + (Clean $desc) + '"')
}
$lines += '}'

$Dist = Join-Path $PSScriptRoot "dist"
New-Item -ItemType Directory -Force $Dist | Out-Null
$Vdf = Join-Path $Dist "workshop_item.vdf"
[System.IO.File]::WriteAllText($Vdf, ($lines -join "`r`n"), (New-Object System.Text.UTF8Encoding($false)))

# ---- 4. upload
# Success is checked against Steam itself: the item's "last updated" time must move.
# (The old check looked for the item id in the VDF, which is always there for an existing
#  item, so a failed upload still printed "Done".)
function Get-ItemUpdated([string]$itemId) {
    # Public Steam Web API, no key needed. Returns the item's last-update time, or 0 if unknown.
    try {
        $r = Invoke-RestMethod -Method Post -Uri "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/" `
            -Body @{ itemcount = 1; "publishedfileids[0]" = $itemId } -TimeoutSec 20
        return [long]$r.response.publishedfiledetails[0].time_updated
    } catch { return 0 }
}
$before = 0
if ($isNew) { Write-Host "==> Creating a new Workshop item ($Visibility)" -ForegroundColor Cyan }
else {
    $before = Get-ItemUpdated $Id
    $when = if ($before -gt 0) { [DateTimeOffset]::FromUnixTimeSeconds($before).LocalDateTime.ToString("yyyy-MM-dd HH:mm") } else { "unknown" }
    Write-Host "==> Updating Workshop item $Id (last updated on Steam: $when)" -ForegroundColor Cyan
}
& $SteamCmd +login $SteamUser +workshop_build_item $Vdf +quit | Tee-Object -Variable steamOut
$steamText = ($steamOut | Out-String)

$after = Get-Content $Vdf -Raw
$newId = $null
if ($after -match '"publishedfileid"\s+"(\d+)"' -and $Matches[1] -ne "0") { $newId = $Matches[1] }

$ok = $false; $how = ""
if ($newId) {
    # Steam can take a few seconds to show the new time.
    for ($i = 0; $i -lt 6 -and -not $ok; $i++) {
        $now = Get-ItemUpdated $newId
        if ($now -gt 0 -and $now -gt $before) { $ok = $true; $how = "Steam shows the item updated at " + [DateTimeOffset]::FromUnixTimeSeconds($now).LocalDateTime.ToString("HH:mm:ss") }
        elseif ($now -eq 0) { break }   # private item or API down: can't check this way
        else { Start-Sleep -Seconds 5 }
    }
    if (-not $ok -and $now -eq 0 -and $steamText -match 'Success' -and $steamText -notmatch 'ERROR|FAILED|Login Failure|Access Denied') {
        $ok = $true; $how = "SteamCMD reported Success (Steam's API couldn't confirm it, e.g. the item is private)"
    }
}

if ($ok) {
    if ($isNew) {
        Set-Content -Path $IdFile -Value $newId -Encoding ASCII
        Write-Host "    saved item id to workshop\publishedfileid.txt - commit it" -ForegroundColor Yellow
    }
    Write-Host ""
    Write-Host " Done: SpawnMenu $Version is on the Workshop. $how" -ForegroundColor Green
    Write-Host " https://steamcommunity.com/sharedfiles/filedetails/?id=$newId" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host " Upload FAILED - the Workshop item did not change. Read the SteamCMD output above." -ForegroundColor Red
    Write-Host " If it shows a login error, Steam Guard prompt or 'Access Denied', log in once and rerun this script:" -ForegroundColor Red
    Write-Host "   & `"$SteamCmd`" +login $SteamUser +quit" -ForegroundColor Yellow
    exit 1
}
