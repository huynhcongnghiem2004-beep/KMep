param([string]$OutputName='KMep.SupplyRouter2024.Final2.dll')
$ErrorActionPreference='Stop'
$revitDir='C:\Program Files\Autodesk\Revit 2024'
$libraryDll=Join-Path (Split-Path $PSScriptRoot -Parent) 'BinhMinhLibrary\BinhMinh_Parametric_Revit2024_mm\BinhMinh.Parametric2024.dll'
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:library "/out:$PSScriptRoot\$OutputName" /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/reference:$revitDir\RevitAPI.dll" "/reference:$revitDir\RevitAPIUI.dll" "/reference:$libraryDll" "$PSScriptRoot\Planner.cs" "$PSScriptRoot\Router.cs" "$PSScriptRoot\App.cs" "$PSScriptRoot\IntegrationTests.cs"
if($LASTEXITCODE -ne 0){throw 'KMep build failed'}


