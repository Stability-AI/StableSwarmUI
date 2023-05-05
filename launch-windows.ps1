# Visual Studio likes to generate invalid files here for some reason, so autonuke it
if (Test-Path "src/Properties/launchSettings.json") {
    rm src/Properties/launchSettings.json
}
# Building first is more reliable than running directly from src
dotnet build
# Default env configuration, gets overwritten by the C# code's settings handler
$Env:ASPNETCORE_ENVIRONMENT = "Production"
$Env:ASPNETCORE_URLS = "http://*:7801"
# Actual runner.
dotnet src\bin\Debug\net7.0\StableUI.dll @args
