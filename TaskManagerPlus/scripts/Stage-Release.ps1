$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location $projectRoot

$dateTag = Get-Date -Format 'yyyy-MM-dd'
$version = '1.0.0'

$sourceDir = Join-Path $projectRoot 'bin\Release'
if (!(Test-Path $sourceDir)) {
  throw "Release output not found: $sourceDir. Run scripts/Build-Release.ps1 first."
}

$stagingDir = Join-Path $projectRoot 'dist\staging'
$portableDir = Join-Path $projectRoot "dist\TaskManagerPlus-win-x64-Release-$version-$dateTag"
$zipPath = Join-Path $projectRoot "dist\TaskManagerPlus-win-x64-Release-$version-$dateTag.zip"

New-Item -ItemType Directory -Force -Path $stagingDir | Out-Null
New-Item -ItemType Directory -Force -Path $portableDir | Out-Null

function Copy-ReleaseFilesTo([string]$destDir) {
  Copy-Item -Force -Path (Join-Path $sourceDir '*.exe') -Destination $destDir
  Copy-Item -Force -Path (Join-Path $sourceDir '*.dll') -Destination $destDir
  Copy-Item -Force -Path (Join-Path $sourceDir '*.config') -Destination $destDir

  Copy-Item -Recurse -Force -Path (Join-Path $sourceDir 'Help') -Destination $destDir
  Copy-Item -Recurse -Force -Path (Join-Path $sourceDir 'Localization') -Destination $destDir
}

Remove-Item -Recurse -Force -Path (Join-Path $stagingDir '*') -ErrorAction SilentlyContinue
Copy-ReleaseFilesTo -destDir $stagingDir

Remove-Item -Recurse -Force -Path (Join-Path $portableDir '*') -ErrorAction SilentlyContinue
Copy-ReleaseFilesTo -destDir $portableDir

if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path (Join-Path $portableDir '*') -DestinationPath $zipPath -Force

Write-Host "STAGING_DIR=$stagingDir"
Write-Host "PORTABLE_DIR=$portableDir"
Write-Host "ZIP=$zipPath"

