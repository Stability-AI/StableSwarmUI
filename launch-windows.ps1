dotnet restore
dotnet build
$Env:ASPNETCORE_ENVIRONMENT = "Development"
$Env:ASPNETCORE_URLS = "http://*:7801"
dotnet src\bin\Debug\net7.0\StableUI.dll
