# KMep / PlumbingPro v0.1 - Revit 2024

## Build
- Visual Studio 2022, .NET Framework 4.8 targeting pack, Revit 2024 installed.
- Open `KMep.PlumbingPro.csproj` and build Release.
- If Revit is not installed at default path, set MSBuild `RevitInstallDir` with path containing RevitAPI.dll and RevitAPIUI.dll.
- This environment has no Revit 2024 assemblies, so this project has **not been compiled or tested inside Revit**.

## Install
1. Create `C:\KMep` and copy `bin\Release\KMep.PlumbingPro.dll` there.
2. Copy `KMep.PlumbingPro.addin` to `%AppData%\Autodesk\Revit\Addins\2024\`.
3. Reopen Revit; expect tab `KMep`, panel `PlumbingPro`, button `Cấp lạnh / Ống nhánh`.
4. To use another DLL location, edit the `<Assembly>` path inside `.addin`.
5. If combining with existing KStruct add-in, do not register two copies of the same command. KMep is a **separate tab**.

## v0.1 behavior and limitations
- Select one existing straight domestic-cold-water main Pipe, then a plumbing FamilyInstance with an unused DomesticColdWater piping connector.
- Project the fixture connector onto the main pipe centerline; only permit a near-collinear branch (connector axis within 11.5 degrees of branch direction).
- Diameter is inherited from the device connector, **NOT a TCVN-computed diameter**.
- Check bounding-box intersection against walls/floors/structural beams/columns, with potential false positives; checks host document only (not linked models).
- Create the branch connected to the fixture with `Pipe.Create`. End is at main axis but **not joined to the main**; do NOT use as an approved working water supply network.
- Main pipe is unmodified. No automatic Tee, no diameter reducer, no elbows, no hydraulic sizing, no precise 3D collision testing, no route search, no TCVN compliance sign-off.
- Nominal `2% slope` field from prior UI is irrelevant to pressurized cold-water routing and is not applied.
- Do not run against production model without backup. Test in a copy.

## Future increments
v0.2: WPF and routing multi-segment with elbows, pipe-type and material controls, tee creation and verified connector connectivity.
v0.3: exact solid collision, links, hydraulic calculations verified against TCVN 4513:1988 and calculation report.
