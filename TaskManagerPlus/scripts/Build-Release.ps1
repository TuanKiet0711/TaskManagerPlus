$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location $projectRoot

$configuration = 'Release'
$platform = 'Any CPU'

Write-Host "Building $configuration..."
dotnet build .\TaskManagerPlus.sln -c $configuration -v minimal

Write-Host "Staging distributable..."
& .\scripts\Stage-Release.ps1

