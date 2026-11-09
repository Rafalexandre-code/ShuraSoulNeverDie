# Packs a Unity Windows build for players, in the two shapes the launcher supports:
#   <Out>\Shura-Windows.zip  -> folder: Shura.exe (launcher, icon) + jogo\ (the build) + the guide
#   <Out>\Shura.exe          -> single file: the whole game inside one executable
#
#   powershell -File Tools\launcher\package.ps1 -Build <Unity build folder> -Out <folder>
param(
    [Parameter(Mandatory = $true)][string]$Build,
    [Parameter(Mandatory = $true)][string]$Out
)
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Resolve-Path (Join-Path $here "..\..")
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$fw = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319"
$guide = Join-Path $repo "docs\COMO_JOGAR_ONLINE.txt"
$icon = Join-Path $here "shura.ico"
$src = Join-Path $here "Launcher.cs"

if (Test-Path $Out) { Remove-Item $Out -Recurse -Force }
New-Item -ItemType Directory $Out | Out-Null
$work = Join-Path $Out "_work"
New-Item -ItemType Directory $work | Out-Null

# The build without debug-only folders.
$game = Join-Path $work "jogo"
Copy-Item $Build $game -Recurse
Get-ChildItem $game -Directory -Recurse | Where-Object { $_.Name -like "*DoNotShip*" } | Remove-Item -Recurse -Force

$refs = @("/r:$fw\System.Windows.Forms.dll", "/r:$fw\System.Drawing.dll", "/r:$fw\System.IO.Compression.dll", "/r:$fw\System.IO.Compression.FileSystem.dll")

# 1) Folder shape.
$folder = Join-Path $work "Shura"
New-Item -ItemType Directory $folder | Out-Null
& $csc /nologo /target:winexe /optimize+ "/win32icon:$icon" "/out:$folder\Shura.exe" $refs $src
if ($LASTEXITCODE -ne 0) { throw "csc (pasta) falhou" }
Move-Item $game (Join-Path $folder "jogo")
Copy-Item $guide (Join-Path $folder "COMO_JOGAR_ONLINE.txt")
Compress-Archive -Path $folder -DestinationPath (Join-Path $Out "Shura-Windows.zip") -CompressionLevel Optimal

# 2) Single file: the build zipped (files at the root) and embedded.
$payload = Join-Path $work "jogo.zip"
Compress-Archive -Path (Join-Path $folder "jogo\*") -DestinationPath $payload -CompressionLevel Optimal
& $csc /nologo /target:winexe /optimize+ "/win32icon:$icon" "/out:$Out\Shura.exe" "/resource:$payload,jogo.zip" $refs $src
if ($LASTEXITCODE -ne 0) { throw "csc (arquivo unico) falhou" }

Remove-Item $work -Recurse -Force
Get-ChildItem $Out | Select-Object Name, @{ n = "MB"; e = { [math]::Round($_.Length / 1MB, 1) } }
