param(
    [ValidateSet("win-x64")]
    [string]$Runtime = "win-x64"
)
$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "CILauncher\CILauncher.csproj"
$built = Join-Path $PSScriptRoot "CILauncher\bin\Release\net48\CIModpackLauncher.exe"
$out = Join-Path $PSScriptRoot "CILauncher\bin\publish"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Path $out | Out-Null
dotnet build $project -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Copy-Item $built (Join-Path $out "CIModpackLauncher.exe")
Write-Host "Launcher exe: $out\CIModpackLauncher.exe"
Write-Host "One file. Windows already has .NET Framework 4.8, so players do not need the runtime beside it."
