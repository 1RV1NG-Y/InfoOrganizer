$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src/InfoOrganizer.Web/InfoOrganizer.Web.csproj'
$output = Join-Path $repoRoot 'artifacts/publish'

dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $output

Write-Host "Published to $output"
