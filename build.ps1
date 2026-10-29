param([ValidateSet('Build','Test','Publish')][string]$Task = 'Build')
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.local/dotnet'
    $env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.packages'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    if ($Task -eq 'Build') { dotnet build EasyScreenshots.sln -c Release }
    elseif ($Task -eq 'Test') { dotnet test EasyScreenshots.sln -c Release }
    else {
        dotnet publish src/EasyScreenshots/EasyScreenshots.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o artifacts/portable
    }
    if ($LASTEXITCODE -ne 0) { throw "dotnet $Task failed ($LASTEXITCODE)." }
} finally { Pop-Location }
