# Ensure correct local path.
$thisPath = Split-Path $MyInvocation.MyCommand.Path -Parent
cd $thisPath
# Visual Studio likes to generate invalid files here for some reason, so autonuke it
if (Test-Path "src/Properties/launchSettings.json") {
    rm src/Properties/launchSettings.json
}
# Building first is more reliable than running directly from src
dotnet build src/StableSwarmUI.csproj --configuration Release -o src/bin/live_release
# Default env configuration, gets overwritten by the C# code's settings handler
$Env:ASPNETCORE_ENVIRONMENT = "Production"
$Env:ASPNETCORE_URLS = "http://*:7801"
# Actual runner.
dotnet src\bin\live_release\StableSwarmUI.dll @args
