[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = Join-Path $projectRoot '.artifacts'
$stagePath = Join-Path $artifactRoot ('release-' + [Guid]::NewGuid().ToString('N'))
$releaseRoot = Join-Path $projectRoot 'release'
$latestPath = Join-Path $releaseRoot 'latest'
$previousPath = Join-Path $artifactRoot 'previous-release'

function Assert-WorkspacePath([string]$Path) {
    $resolved = [IO.Path]::GetFullPath($Path)
    if (-not $resolved.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Path outside workspace: $resolved" }
    # Reject junctions in the complete path, including parent directories.
    $currentPath = $resolved
    while ($currentPath -ne $projectRoot) {
        if ((Test-Path -LiteralPath $currentPath) -and ((Get-Item -LiteralPath $currentPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Refusing a junction: $currentPath" }
        $currentPath = [IO.Path]::GetDirectoryName($currentPath)
    }
}
function Remove-WorkspaceDirectory([string]$Path) {
    Assert-WorkspacePath $Path
    if (Test-Path -LiteralPath $Path) { Remove-Item -LiteralPath $Path -Recurse -Force }
}

Push-Location $projectRoot
try {
    Assert-WorkspacePath $stagePath
    New-Item -ItemType Directory -Path $stagePath -Force | Out-Null
    dotnet build DiskOptimizer.csproj -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
    # DriverTests does not contact the catalogue unless explicitly passed --live.
    foreach ($suite in @('Aetherial.Regression', 'Aetherial.DriverTests', 'Aetherial.ScanTests')) {
        dotnet run --project "tests/$suite/$suite.csproj" -c Release
        if ($LASTEXITCODE -ne 0) { throw "$suite offline checks failed; previous release preserved." }
    }
    dotnet run --project tests/Aetherial.VisualSmoke/Aetherial.VisualSmoke.csproj -c Release -- --interactions-only
    if ($LASTEXITCODE -ne 0) { throw 'WPF interaction checks failed; previous release preserved.' }
    dotnet publish DiskOptimizer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o $stagePath --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed; previous release preserved.' }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $stagePath
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/RELEASE_NOTES.md') -Destination $stagePath
    [xml]$project = Get-Content -LiteralPath (Join-Path $projectRoot 'DiskOptimizer.csproj')
    $version = [string]$project.Project.PropertyGroup.Version
    $exe = Get-Item -LiteralPath (Join-Path $stagePath 'Aetherial.exe')
    if ($exe.Length -lt 10MB) { throw 'Unexpectedly small self-contained executable.' }
    @{
        version = $version
        runtime = 'win-x64'
        builtUtc = [DateTime]::UtcNow.ToString('o')
        executableSha256 = (Get-FileHash -LiteralPath $exe.FullName -Algorithm SHA256).Hash
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stagePath 'release.json') -Encoding utf8
    New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
    foreach ($candidate in @($stagePath, $latestPath, $previousPath)) { Assert-WorkspacePath $candidate }
    Remove-WorkspaceDirectory $previousPath
    if (Test-Path -LiteralPath $latestPath) { Move-Item -LiteralPath $latestPath -Destination $previousPath }
    try { Move-Item -LiteralPath $stagePath -Destination $latestPath }
    catch {
        if (Test-Path -LiteralPath $previousPath) { Move-Item -LiteralPath $previousPath -Destination $latestPath }
        throw
    }
    Remove-WorkspaceDirectory $previousPath
    foreach ($oldPackage in Get-ChildItem -LiteralPath $releaseRoot -Force | Where-Object Name -ne 'latest') {
        Assert-WorkspacePath $oldPackage.FullName
        Remove-Item -LiteralPath $oldPackage.FullName -Recurse -Force
    }
    $buildDirectories = @('bin', 'obj', 'publish')
    foreach ($buildProject in @('Aetherial.Core', 'Aetherial.Services', 'tests/Aetherial.Regression', 'tests/Aetherial.DriverTests', 'tests/Aetherial.ScanTests', 'tests/Aetherial.VisualSmoke')) {
        $buildDirectories += "$buildProject/bin", "$buildProject/obj"
    }
    foreach ($buildDirectory in $buildDirectories) {
        Remove-WorkspaceDirectory (Join-Path $projectRoot $buildDirectory)
    }
    $legacyFiles = @('Aetherial_old.exe','Aetherial.exe','DiskOptimizer_old.exe','DiskOptimizer.exe','DiskOptimizer.dll','DiskOptimizer.pdb','DiskOptimizer.deps.json','DiskOptimizer.runtimeconfig.json','D3DCompiler_47_cor3.dll','PenImc_cor3.dll','PresentationNative_cor3.dll','vcruntime140_cor3.dll','wpfgfx_cor3.dll','crash.log')
    foreach ($legacy in $legacyFiles) {
        $legacyPath = Join-Path $projectRoot $legacy
        Assert-WorkspacePath $legacyPath
        if (Test-Path -LiteralPath $legacyPath) { Remove-Item -LiteralPath $legacyPath -Force }
    }
    Write-Host "Release $version ready: $latestPath"
}
finally {
    Remove-WorkspaceDirectory $stagePath
    Pop-Location
}
