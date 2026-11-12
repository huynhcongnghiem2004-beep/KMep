param([switch]$BuilderOnly)
$ErrorActionPreference='Stop'
$libraryRoot=Split-Path -Parent $PSScriptRoot
$compiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$revitRoot='C:\Program Files\Autodesk\Revit 2024'
$refs=@("/reference:$revitRoot\RevitAPI.dll","/reference:$revitRoot\RevitAPIUI.dll")
if(-not $BuilderOnly){
 & $compiler /nologo /target:library "/out:$libraryRoot\BinhMinh.Parametric2024.dll" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll @refs "$PSScriptRoot\parametric\Addin.cs" "$PSScriptRoot\parametric\BoundedConnect.cs"
 if($LASTEXITCODE -ne 0){throw 'Add-in compile failed; close Revit if the DLL is locked'}
}
& $compiler /nologo /target:library "/out:$libraryRoot\ParametricBuilder.dll" @refs "/reference:$libraryRoot\BinhMinh.Parametric2024.dll" "$PSScriptRoot\ParametricBuilder.cs" "$PSScriptRoot\ParametricFamilies.cs" "$PSScriptRoot\ParametricVerification.cs"
if($LASTEXITCODE -ne 0){throw 'Builder compile failed'}
