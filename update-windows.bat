@echo off

rem Ensure correct local path.
cd /D "%~dp0"

rem Microsoft borked the dotnet installer/path handler, so force x64 to be read first
set PATH=C:\Program Files\dotnet;%PATH%

rem For some reason Microsoft's nonsense is missing the official nuget source? So forcibly add that to be safe.
dotnet nuget add source https://api.nuget.org/v3/index.json --name "NuGet official package source"

rem The actual update
git pull

rem Make a backup of the current live_release to be safe
if exist src\bin\live_release\ (
    rmdir /s /q src\bin\live_release_backup
    move src\bin\live_release src\bin\live_release_backup
)

rem Now build the new copy
dotnet build src/StableSwarmUI.csproj --configuration Release -o src/bin/live_release

timeout 3
