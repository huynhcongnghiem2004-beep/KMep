$ErrorActionPreference='Stop'
$manifest=Join-Path $env:APPDATA 'Autodesk\Revit\Addins\2024\KMep.PlumbingPro.addin'
$dll=Join-Path $PSScriptRoot 'KMep.SupplyRouter2024.Final2.dll'
if(!(Test-Path -LiteralPath $dll)){throw 'Missing Final DLL'}
if(!(Test-Path -LiteralPath $manifest)){throw 'Existing KMep manifest not found'}
[xml]$xml=Get-Content -LiteralPath $manifest -Raw
if($xml.RevitAddIns.AddIn.AddInId -ne '182B8A1E-9045-45D9-B47C-3E619067A2A4'){throw 'Unexpected KMep add-in identity'}
$backup=Join-Path $PSScriptRoot ('KMep.PlumbingPro.before-'+(Get-Date -Format yyyyMMdd-HHmmss)+'.addin.backup')
Copy-Item -LiteralPath $manifest -Destination $backup
$xml.RevitAddIns.AddIn.Assembly=$dll
$xml.RevitAddIns.AddIn.FullClassName='KMep.Supply.App'
$xml.RevitAddIns.AddIn.VendorDescription='KMep Supply Router 3D - Revit 2024 mm'
$xml.Save($manifest)
Write-Output "Installed KMep Supply Router. Previous manifest: $backup"


