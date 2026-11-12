$ErrorActionPreference='Stop'
$libraryRoot=Split-Path -Parent $PSScriptRoot
$packageRoot=Join-Path $libraryRoot 'BinhMinh_Parametric_Revit2024_mm'
$flex=Import-Csv (Join-Path $libraryRoot 'output\ParametricFlexTests.csv')
$connections=Import-Csv (Join-Path $libraryRoot 'output\ParametricConnectionTests.csv')
if($flex.Count -ne 159 -or @($flex | Where-Object Result -ne PASS).Count){throw 'Family flex checks are incomplete'}
if($connections.Count -ne 326 -or @($connections | Where-Object Result -ne PASS).Count){throw 'Connection/type-change checks are incomplete'}
$plan=Import-Csv (Join-Path $libraryRoot 'catalogue\ParametricTypePlan.csv')
$families=@($plan | Select-Object -ExpandProperty Family -Unique)
if($families.Count -ne 14 -or $plan.Count -ne 131){throw 'Unexpected family/type plan'}
foreach($part in @('catalogue','output','output\FamiliesParametric','source','source\parametric')){New-Item -ItemType Directory -Path (Join-Path $packageRoot $part) -Force | Out-Null}
foreach($name in $families){Copy-Item -LiteralPath (Join-Path $libraryRoot ('output\FamiliesParametric\'+$name+'.rfa')) -Destination (Join-Path $packageRoot 'output\FamiliesParametric')}
Copy-Item -LiteralPath (Join-Path $libraryRoot 'output\BMP_Parametric_Revit2024_mm_v2.rvt') -Destination (Join-Path $packageRoot 'output\BMP_Parametric_Revit2024_mm.rvt')
foreach($name in @('ParametricFlexTests.csv','ParametricConnectionTests.csv')){Copy-Item -LiteralPath (Join-Path $libraryRoot ('output\'+$name)) -Destination (Join-Path $packageRoot 'output')}
Get-ChildItem -LiteralPath (Join-Path $libraryRoot 'output') -Filter 'ParametricPreview*.png' | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $packageRoot 'output')}
foreach($name in @('pipes.csv','fittings.csv','ParametricTypePlan.csv')){Copy-Item -LiteralPath (Join-Path $libraryRoot ('catalogue\'+$name)) -Destination (Join-Path $packageRoot 'catalogue')}
Copy-Item -LiteralPath (Join-Path $libraryRoot 'README_Parametric.md') -Destination (Join-Path $packageRoot 'README.md')
foreach($name in @('Install-Parametric-Revit2024.ps1','BinhMinh.Parametric2024.dll')){Copy-Item -LiteralPath (Join-Path $libraryRoot $name) -Destination $packageRoot}
foreach($name in @('ParametricBootstrap.cs','ParametricBuilder.cs','ParametricFamilies.cs','ParametricVerification.cs','Build-Parametric.ps1','Package-Parametric.ps1')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $packageRoot 'source')}
foreach($name in @('Addin.cs','BoundedConnect.cs','BinhMinh.Parametric2024.csproj')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('parametric\'+$name)) -Destination (Join-Path $packageRoot 'source\parametric')}
[ordered]@{Version='2.0-parametric';Revit='2024';Units='mm';FamilyFiles=14;FamilyTypes=131;FamilyFlexChecks=159;ProjectChecks=326;TotalPassed=485;BuiltAt=(Get-Date -Format o);NativeGeometry='Dimension-driven revolutions, extrusions and blends';RuntimeAssembly='BinhMinh.Parametric2024.dll'} | ConvertTo-Json | Set-Content (Join-Path $packageRoot 'BuildInfo.json') -Encoding utf8
$hashes=Get-ChildItem -LiteralPath $packageRoot -File -Recurse | Where-Object Name -ne SHA256SUMS.txt | Sort-Object FullName | ForEach-Object {((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant())+'  '+$_.FullName.Substring($packageRoot.Length+1).Replace('\','/')}
$hashes | Set-Content -LiteralPath (Join-Path $packageRoot 'SHA256SUMS.txt') -Encoding ascii
$zip=Join-Path $libraryRoot 'BinhMinh_Parametric_Revit2024_mm.zip'
if(Test-Path -LiteralPath $zip){$archive=Join-Path $libraryRoot 'previous';New-Item -ItemType Directory -Path $archive -Force | Out-Null;Move-Item -LiteralPath $zip -Destination (Join-Path $archive ('BinhMinh_Parametric_'+(Get-Date -Format yyyyMMddHHmmssfff)+'.zip'))}
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($packageRoot,$zip,[System.IO.Compression.CompressionLevel]::Optimal,$false)
$z=[System.IO.Compression.ZipFile]::OpenRead($zip)
try{
 $rfas=@($z.Entries | Where-Object FullName -like 'output/FamiliesParametric/*.rfa')
 if($rfas.Count -ne 14){throw 'ZIP must contain exactly 14 family files'}
 foreach($line in $hashes){$pair=$line -split '  ',2;$entry=$z.GetEntry($pair[1]);if($null -eq $entry){throw ('Missing ZIP member '+$pair[1])};$stream=$entry.Open();$sha=[System.Security.Cryptography.SHA256]::Create();try{$actual=([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-','').ToLowerInvariant();if($actual -ne $pair[0]){throw ('ZIP hash mismatch '+$pair[1])}}finally{$stream.Dispose();$sha.Dispose()}}
 Write-Output ('VERIFIED: '+$z.Entries.Count+' files, 14 families, 131 types, 485 PASS; ZIP '+(Get-Item -LiteralPath $zip).Length+' bytes')
}finally{$z.Dispose()}
