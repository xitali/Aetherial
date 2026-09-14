[CmdletBinding()]
param([switch]$Uninstall)
$ErrorActionPreference = 'Stop'
$installPath = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\Aetherial'))
$registryPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Aetherial'
$shortcuts = @((Join-Path ([Environment]::GetFolderPath('Desktop')) 'Aetherial.lnk'), (Join-Path ([Environment]::GetFolderPath('Programs')) 'Aetherial.lnk'))
$files = @('Aetherial.exe','DiskOptimizer_Manual.html','LICENSE','RELEASE_NOTES.md','release.json')
# Refuse redirected directories before copying or removing known application files.
$candidate = $installPath
while ($candidate) {
    if ((Test-Path -LiteralPath $candidate) -and ((Get-Item -LiteralPath $candidate -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Redirected install path: $candidate" }
    $candidate = [IO.Path]::GetDirectoryName($candidate)
}
$running = @(Get-Process Aetherial -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $installPath 'Aetherial.exe') })
if ($running.Count) { throw 'Close the installed Aetherial before replacing or uninstalling it.' }
if ($Uninstall) {
    foreach ($name in ($files + 'uninstall.ps1')) {
        $target = [IO.Path]::GetFullPath((Join-Path $installPath $name))
        if ([IO.Path]::GetDirectoryName($target) -ne $installPath) { throw 'Invalid removal target' }
        if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force }
    }
    $shell = New-Object -ComObject WScript.Shell
    foreach ($link in $shortcuts) {
        if ((Test-Path -LiteralPath $link) -and $shell.CreateShortcut($link).TargetPath -eq (Join-Path $installPath 'Aetherial.exe')) { Remove-Item -LiteralPath $link -Force }
    }
    if (Test-Path -LiteralPath $registryPath) { Remove-Item -LiteralPath $registryPath }
    if ((Test-Path -LiteralPath $installPath) -and !(Get-ChildItem -LiteralPath $installPath -Force)) { Remove-Item -LiteralPath $installPath }
    Write-Host 'Aetherial removed. User settings and history retained.'
    return
}
$packagePath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\release\latest'))
$metadata = Get-Content -LiteralPath (Join-Path $packagePath 'release.json') | ConvertFrom-Json
if ((Get-FileHash -LiteralPath (Join-Path $packagePath 'Aetherial.exe')).Hash -ne $metadata.executableSha256) { throw 'Package checksum mismatch' }
foreach ($name in $files) { if (!(Test-Path -LiteralPath (Join-Path $packagePath $name) -PathType Leaf)) { throw "Missing package file: $name" } }
New-Item -ItemType Directory -Path $installPath -Force | Out-Null
foreach ($name in $files) { Copy-Item -LiteralPath (Join-Path $packagePath $name) -Destination (Join-Path $installPath $name) -Force }
Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $installPath 'uninstall.ps1') -Force
if ((Get-FileHash -LiteralPath (Join-Path $installPath 'Aetherial.exe')).Hash -ne $metadata.executableSha256) { throw 'Installed checksum mismatch' }
$shell = New-Object -ComObject WScript.Shell
foreach ($link in $shortcuts) {
    $shortcut = $shell.CreateShortcut($link)
    $shortcut.TargetPath = Join-Path $installPath 'Aetherial.exe'
    $shortcut.WorkingDirectory = $installPath
    $shortcut.IconLocation = $shortcut.TargetPath + ',0'
    $shortcut.Description = 'Aetherial - dyski, czyszczenie i sterowniki'
    $shortcut.Save()
}
New-Item -Path $registryPath -Force | Out-Null
$uninstallCommand = 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $installPath 'uninstall.ps1') + '" -Uninstall'
foreach ($pair in @{DisplayName='Aetherial';DisplayVersion=$metadata.version;Publisher='Aetherial';InstallLocation=$installPath;DisplayIcon=(Join-Path $installPath 'Aetherial.exe');UninstallString=$uninstallCommand}.GetEnumerator()) {
    New-ItemProperty -Path $registryPath -Name $pair.Key -Value $pair.Value -PropertyType String -Force | Out-Null
}
New-ItemProperty -Path $registryPath -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $registryPath -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null
Write-Host "Installed Aetherial $($metadata.version): $installPath"
