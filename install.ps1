param(
    [string]$TargetDir = (Join-Path $env:LOCALAPPDATA 'DeepSeekWhale'),
    [switch]$NoShortcut,
    [switch]$NoAutoStart,
    [switch]$NoLaunch
)
$ErrorActionPreference = 'Stop'

$src = Join-Path $PSScriptRoot 'DeepSeekWhale.exe'
if (-not (Test-Path $src)) { $src = Join-Path $PSScriptRoot 'dist\DeepSeekWhale.exe' }
if (-not (Test-Path $src)) { throw "找不到 DeepSeekWhale.exe，请把 exe 放在 install.ps1 旁边，或先运行 build.ps1" }

New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null
$exe = Join-Path $TargetDir 'DeepSeekWhale.exe'
Copy-Item $src $exe -Force
Write-Host "已安装到 $TargetDir"

$ws = New-Object -ComObject WScript.Shell

if (-not $NoShortcut) {
    $desktop = [Environment]::GetFolderPath('Desktop')
    $lnkPath = Join-Path $desktop 'DeepSeek 小鲸鱼.lnk'
    $lnk = $ws.CreateShortcut($lnkPath)
    $lnk.TargetPath = $exe
    $lnk.WorkingDirectory = $TargetDir
    $lnk.IconLocation = $exe + ',0'
    $lnk.Description = 'DeepSeek 余额小鲸鱼'
    $lnk.Save()
    Write-Host "已创建桌面快捷方式：$lnkPath"
}

if (-not $NoAutoStart) {
    $startup = [Environment]::GetFolderPath('Startup')
    $autoPath = Join-Path $startup 'DeepSeek 小鲸鱼.lnk'
    $auto = $ws.CreateShortcut($autoPath)
    $auto.TargetPath = $exe
    $auto.Arguments = '--autostart'
    $auto.WorkingDirectory = $TargetDir
    $auto.IconLocation = $exe + ',0'
    $auto.Description = 'DeepSeek 余额小鲸鱼（开机自启）'
    $auto.Save()
    Write-Host "已设置开机自启：$autoPath"
}

if (-not $NoLaunch) {
    Start-Process -FilePath $exe
    Write-Host "已启动 DeepSeek 小鲸鱼"
}

Write-Host ""
Write-Host "下一步：点小鲸鱼或托盘图标 → 设置，把 API Key 粘贴进去。"
Write-Host "数据目录：$env:APPDATA\DeepSeekWhale"
