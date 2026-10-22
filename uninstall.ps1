param(
    [string]$TargetDir = (Join-Path $env:LOCALAPPDATA 'DeepSeekWhale'),
    [switch]$PurgeData
)
$ErrorActionPreference = 'Continue'

Get-Process DeepSeekWhale -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 400

$autoPath = Join-Path ([Environment]::GetFolderPath('Startup')) 'DeepSeek 小鲸鱼.lnk'
if (Test-Path $autoPath) {
    Remove-Item $autoPath -Force
    Write-Host "已移除开机自启快捷方式"
}

$run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if (Test-Path $run) {
    Remove-ItemProperty -Path $run -Name 'DeepSeekWhale' -ErrorAction SilentlyContinue
}

$lnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'DeepSeek 小鲸鱼.lnk'
if (Test-Path $lnk) {
    Remove-Item $lnk -Force
    Write-Host "已删除桌面快捷方式"
}

if (Test-Path $TargetDir) {
    Remove-Item $TargetDir -Recurse -Force
    Write-Host "已删除程序目录 $TargetDir"
}

$data = Join-Path $env:APPDATA 'DeepSeekWhale'
if ($PurgeData) {
    if (Test-Path $data) {
        Remove-Item $data -Recurse -Force
        Write-Host "已删除本地数据（Key、历史记录）"
    }
} else {
    Write-Host "保留本地数据：$data（如需删除请加 -PurgeData 再运行一次）"
}

Write-Host "卸载完成。"
