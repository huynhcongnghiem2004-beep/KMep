$ErrorActionPreference = 'Stop'
$repo = 'https://github.com/huynhcongnghiem2004-beep/KMep.git'
$zip = Join-Path $PSScriptRoot 'KMep_GitHub_Ready.zip'
$folder = Join-Path $PSScriptRoot 'KMep_Source'
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw 'Chua cai Git. Hay cai Git for Windows va thu lai.' }
if (-not (Test-Path $zip)) { throw "Khong tim thay $zip" }
if (Test-Path $folder) { throw "Thu muc $folder da ton tai. Hay doi ten hoac di chuyen thu muc nay de tranh ghi de." }
New-Item -ItemType Directory -Path $folder | Out-Null
Expand-Archive -LiteralPath $zip -DestinationPath $folder
Set-Location $folder
& git init -b main
if ($LASTEXITCODE -ne 0) { throw 'git init failed' }
& git remote add origin $repo
& git add --all
& git commit -m 'Initial import KMep PlumbingPro Revit 2024'
if ($LASTEXITCODE -ne 0) { throw 'Git commit failed. Kiem tra git config user.name va user.email.' }
& git push -u origin main
if ($LASTEXITCODE -ne 0) { throw 'Git push failed. Kiem tra quyen truy cap, dang nhap GitHub hoac repo da co commit.' }
Write-Host 'Upload thanh cong: https://github.com/huynhcongnghiem2004-beep/KMep' -ForegroundColor Green
