@echo off
setlocal ENABLEDELAYEDEXPANSION

rem Ensure correct local path.
cd /D "%~dp0"

rem Microsoft borked the dotnet installer/path handler, so force x64 to be read first
set PATH=C:\Program Files\dotnet;%PATH%

if not exist .git (
    echo "" & echo ""
    echo "WARNING: YOU DID NOT CLONE FROM GIT. THIS WILL BREAK SOME SYSTEMS. PLEASE INSTALL PER THE README."
    echo "" & echo ""
) else (
    for /f "delims=" %%i in ('git rev-parse HEAD') do set CUR_HEAD=%%i
    set /p BUILT_HEAD=<src/bin/last_build
    if not "!CUR_HEAD!"=="!BUILT_HEAD!" (
        echo "" & echo ""
        echo "WARNING: You did a git pull without building. Will now build for you..."
        echo "" & echo ""
        rmdir /s /q .\src\bin\live_release_backup
        move .\src\bin\live_release .\src\bin\live_release_backup
    )
)

rem Build the program if it isn't already built
if not exist src\bin\live_release\StableSwarmUI.dll (
    rem For some reason Microsoft's nonsense is missing the official nuget source? So forcibly add that to be safe.
    dotnet nuget add source https://api.nuget.org/v3/index.json --name "NuGet official package source"

    dotnet build src/StableSwarmUI.csproj --configuration Release -o src/bin/live_release
    for /f "delims=" %%i in ('git rev-parse HEAD') do set CUR_HEAD2=%%i
    echo !CUR_HEAD2!> src/bin/last_build
)

rem Default env configuration, gets overwritten by the C# code's settings handler
set ASPNETCORE_ENVIRONMENT="Production"
set ASPNETCORE_URLS="http://*:7801"

dotnet src\bin\live_release\StableSwarmUI.dll %*

IF %ERRORLEVEL% NEQ 0 ( pause )
