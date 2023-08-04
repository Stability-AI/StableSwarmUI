@echo off

cd "%~dp0"

rem Microsoft borked the dotnet installer/path handler, so force x64 to be read first
set PATH=C:\Program Files\dotnet;%PATH%

dotnet build src/StableSwarmUI.csproj --configuration Release -o src/bin/live_release

set ASPNETCORE_ENVIRONMENT="Production"
set ASPNETCORE_URLS="http://*:7801"

dotnet src\bin\live_release\StableSwarmUI.dll %*

IF %ERRORLEVEL% NEQ 0 ( pause )
