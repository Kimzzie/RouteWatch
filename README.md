# RouteWatch (PT6: verified, repaired and published)

Prepared by: Kimberly Bautista and <partner name> · BSIT-3D

## Requirements
Windows x64. The .NET 10 SDK is needed only to build and test; the published app does not need .NET installed.

## Build, test, run
```powershell
dotnet restore RouteWatch.sln
dotnet build RouteWatch.sln
dotnet test RouteWatch.sln        # 14 tests: TC-01 to TC-08 plus regression guards
dotnet run --project RouteWatch.Desktop
```

## Configuration
`appsettings.json` (next to the executable) sets `ApiBaseAddress` and `DefaultBranch`. Both can be edited in the window for the current session. If the file cannot be read, the status bar says so and the fields start empty.
Saved data: `%LOCALAPPDATA%\RouteWatch-Student\shipments.db` (delete this file for a clean start).

## HubLink service (manual checks only)
Run `HubLink-Local-Server\Start-HubLink.cmd` (use `Start-HubLink-5081.cmd` if port 5080 is busy). The automated tests do not need it.

## Publish
```powershell
dotnet publish .\RouteWatch.Desktop\RouteWatch.Desktop.csproj -c Release -r win-x64 --self-contained true -o .\publish\win-x64
```
Run `publish\win-x64\RouteWatch.Desktop.exe`. Copy the entire folder.

## Known notes
- The build shows warning NU1903 for `Tmds.DBus.Protocol` (a transitive dependency of Avalonia, not part of this task, not changed).

## Acknowledgement
AI assistance (Claude) was used for guidance on test design and fixes. We reviewed, ran and can explain all changes.