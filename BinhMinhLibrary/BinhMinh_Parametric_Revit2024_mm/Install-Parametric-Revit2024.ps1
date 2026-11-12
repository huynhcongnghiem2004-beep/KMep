$ErrorActionPreference='Stop'
$libraryRoot=$PSScriptRoot
$assembly=Join-Path $libraryRoot 'BinhMinh.Parametric2024.dll'
if(-not(Test-Path -LiteralPath $assembly)){throw 'Khong tim thay DLL. Giai nen day du goi thu vien truoc khi cai.'}
$addinFolder=Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Autodesk\Revit\Addins\2024'
New-Item -ItemType Directory -Path $addinFolder -Force | Out-Null
$manifest=Join-Path $addinFolder 'BinhMinh.AutoConnect2024.addin'
if(Test-Path -LiteralPath $manifest){
 [xml]$old=Get-Content -LiteralPath $manifest -Raw
 if($old.RevitAddIns.AddIn.AddInId -ne 'B16DFEC2-E42E-4A91-AEA9-D831FD22C8E1'){throw 'Manifest hien tai khong phai bo Binh Minh nay. Dung de tranh ghi de add-in khac.'}
 $backupFolder=Join-Path $libraryRoot 'previous'
 New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null
 Copy-Item -LiteralPath $manifest -Destination (Join-Path $backupFolder ('BinhMinh.AutoConnect2024.'+(Get-Date -Format yyyyMMddHHmmssfff)+'.addin'))
}
$safeAssembly=[System.Security.SecurityElement]::Escape($assembly)
@"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns><AddIn Type="Application"><Name>BinhMinh Parametric 2024</Name><Assembly>$safeAssembly</Assembly><AddInId>B16DFEC2-E42E-4A91-AEA9-D831FD22C8E1</AddInId><FullClassName>BinhMinh.ParametricConnect.App</FullClassName><VendorId>KMEP</VendorId><VendorDescription>Binh Minh multi-type parametric fitting library</VendorDescription></AddIn></RevitAddIns>
"@ | Set-Content -LiteralPath $manifest -Encoding UTF8
Write-Host 'Da dang ky ban nhieu Type. Mo lai Revit 2024, vao KMep > Binh Minh Parametric.'
