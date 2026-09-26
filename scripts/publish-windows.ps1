$ErrorActionPreference = "Stop"
if ($env:OS -ne "Windows_NT") {
    Write-Error "Publish the WPF app from Windows 10 or Windows 11 x64."
    exit 1
}

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\GLOptimizer.App\GLOptimizer.App.csproj"
$publish = Join-Path $root "publish"
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $publish --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Published $publish\GLOptimizer.exe"
