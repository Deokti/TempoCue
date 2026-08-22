[CmdletBinding()]
param(
    [string] $InnoCompilerPath,

    # Веса сторонней модели обучены через Ultralytics с лицензией AGPL-3.0.
    # Выпуск, который куда-то передаётся, — это распространение, поэтому по
    # умолчанию веса в него не попадают. Ключ нужен только для сборки, которая
    # останется на своих машинах.
    [switch] $IncludeExperimentalModel
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$propertiesPath = Join-Path $repositoryRoot 'Directory.Build.props'
$applicationProject = Join-Path $repositoryRoot 'src\TempoCue.App\TempoCue.App.csproj'
$publishProfile = Join-Path $repositoryRoot 'src\TempoCue.App\Properties\PublishProfiles\win-x64.pubxml'
$installerScript = Join-Path $repositoryRoot 'installer\TempoCue.iss'
$releaseRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts\release'))

[xml] $properties = Get-Content -LiteralPath $propertiesPath -Raw
$version = [string] $properties.Project.PropertyGroup.Version

if ([string]::IsNullOrWhiteSpace($version)) {
    throw 'Version was not found in Directory.Build.props.'
}

$versionOutputDirectory = [System.IO.Path]::GetFullPath((Join-Path $releaseRoot $version))
$publishDirectory = Join-Path $versionOutputDirectory 'publish'
$zipPath = Join-Path $versionOutputDirectory "TempoCue-$version-win-x64.zip"
$checksumPath = Join-Path $versionOutputDirectory 'SHA256SUMS.txt'

$releaseRootWithSeparator = $releaseRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $versionOutputDirectory.StartsWith($releaseRootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Release output must be located inside artifacts\release.'
}

if (Test-Path -LiteralPath $versionOutputDirectory) {
    Remove-Item -LiteralPath $versionOutputDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

Write-Host 'Restoring dependencies for win-x64...'
dotnet restore $applicationProject --runtime win-x64
if ($LASTEXITCODE -ne 0) {
    throw 'Application dependency restore failed.'
}

Write-Host "Publishing TempoCue $version..."
dotnet publish $applicationProject `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --no-restore `
    --output $publishDirectory `
    -p:PublishProfile=$publishProfile `
    "-p:IncludeExperimentalModel=$($IncludeExperimentalModel.IsPresent.ToString().ToLowerInvariant())"
if ($LASTEXITCODE -ne 0) {
    throw 'Application publish failed.'
}

Write-Host 'Creating portable ZIP...'
Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $zipPath -CompressionLevel Optimal

# Сжатие уже однажды завершилось молча, не создав архив, и выпуск оказался
# неполным. Отсутствие файла должно ломать сборку, а не обнаруживаться потом.
if (-not (Test-Path -LiteralPath $zipPath)) {
    throw "Portable ZIP was not created at $zipPath."
}

if ([string]::IsNullOrWhiteSpace($InnoCompilerPath)) {
    $compilerCandidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )

    $InnoCompilerPath = $compilerCandidates |
        Where-Object { Test-Path -LiteralPath $_ } |
        Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($InnoCompilerPath) -or -not (Test-Path -LiteralPath $InnoCompilerPath)) {
    throw 'Inno Setup 6 compiler was not found. Install Inno Setup or pass -InnoCompilerPath.'
}

Write-Host 'Creating installer...'
& $InnoCompilerPath `
    "/DAppVersion=$version" `
    "/DPublishDir=$publishDirectory" `
    "/DOutputDir=$versionOutputDirectory" `
    $installerScript
if ($LASTEXITCODE -ne 0) {
    throw 'Installer build failed.'
}

$releaseFiles = @(
    $zipPath,
    (Join-Path $versionOutputDirectory "TempoCue-Setup-$version.exe")
)

$checksums = $releaseFiles | ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256
    '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), (Split-Path -Leaf $_)
}
$checksums | Set-Content -LiteralPath $checksumPath -Encoding utf8

Write-Host ''
Write-Host "TempoCue $version release artifacts:"
$releaseFiles + $checksumPath | ForEach-Object { Write-Host "  $_" }
