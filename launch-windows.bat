@echo off

cd "%~dp0"

dotnet build src/StableSwarmUI.csproj --configuration Release -o src/bin/live_release

set ASPNETCORE_ENVIRONMENT="Production"
set ASPNETCORE_URLS="http://*:7801"

dotnet src\bin\live_release\StableSwarmUI.dll %*
