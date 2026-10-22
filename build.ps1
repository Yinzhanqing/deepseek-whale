param(
    [switch]$Console,
    [switch]$Quiet
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }

$outDir = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$out = Join-Path $outDir 'DeepSeekWhale.exe'
$target = if ($Console) { 'exe' } else { 'winexe' }

$sources = @(Get-ChildItem (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$refs = @(
    'System.dll',
    'System.Core.dll',
    'System.Drawing.dll',
    'System.Windows.Forms.dll',
    'System.Web.Extensions.dll',
    'System.Security.dll'
)
$cscArgs = New-Object System.Collections.Generic.List[string]
[void]$cscArgs.Add('/nologo')
[void]$cscArgs.Add('/target:' + $target)
[void]$cscArgs.Add('/platform:anycpu')
[void]$cscArgs.Add('/codepage:65001')
[void]$cscArgs.Add('/optimize+')
[void]$cscArgs.Add('/win32manifest:' + (Join-Path $root 'app.manifest'))
[void]$cscArgs.Add('/out:' + $out)

$icon = Join-Path $root 'assets\whale.ico'
if (Test-Path $icon) { [void]$cscArgs.Add('/win32icon:' + $icon) }
foreach ($r in $refs) { [void]$cscArgs.Add('/r:' + $r) }
foreach ($s in $sources) { [void]$cscArgs.Add($s) }

& $csc $cscArgs.ToArray()
if ($LASTEXITCODE -ne 0) { throw "compile failed with exit code $LASTEXITCODE" }
if (-not $Quiet) {
    $fi = Get-Item $out
    Write-Host ("BUILD OK  " + $fi.FullName + "  " + [math]::Round($fi.Length / 1KB, 1) + " KB")
}
