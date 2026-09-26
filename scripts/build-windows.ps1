$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
dotnet test (Join-Path $root "GLOptimizer.sln") -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
