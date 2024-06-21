# Ensure correct local path.
Set-Location -Path $PSScriptRoot

# Microsoft borked the dotnet installer/path handler, so force x64 to be read first
$env:PATH = "C:\Program Files\dotnet;$env:PATH"

if (!(Test-Path .git)) {
    Write-Host "" 
    Write-Host "WARNING: YOU DID NOT CLONE FROM GIT. THIS WILL BREAK SOME SYSTEMS. PLEASE INSTALL PER THE README."
    Write-Host "" 
} else {
    $CUR_HEAD = git rev-parse HEAD
    if (!(Test-Path src\bin\last_build)) {
        $BUILT_HEAD = 0
    } else {
        $BUILT_HEAD = Get-Content src\bin\last_build
    }
    if ($CUR_HEAD -ne $BUILT_HEAD) {
        Write-Host ""
        Write-Host "WARNING: You did a git pull without building. Will now build for you..."
        Write-Host ""
        if (Test-Path .\src\bin\live_release_backup) {
            Remove-Item -Path .\src\bin\live_release_backup -Recurse -Force
        }
        if (Test-Path .\src\bin\live_release_backup) {
            Move-Item -Path .\src\bin\live_release -Destination .\src\bin\live_release_backup
        }
    }
}

# Build the program if it isn't already built
if (!(Test-Path src\bin\live_release\StableSwarmUI.dll)) {
    # For some reason Microsoft's nonsense is missing the official nuget source? So forcibly add that to be safe.
    dotnet nuget add source https://api.nuget.org/v3/index.json --name "NuGet official package source"

    dotnet publish src/StableSwarmUI.csproj -c Release -r win-x64 -o .\ -p:PublishSingleFile=true --self-contained true

    $CUR_HEAD2 = git rev-parse HEAD
    if (Test-Path src/bin/last_build) {
        $CUR_HEAD2 | Out-File -FilePath src/bin/last_build
    }
}

# Default env configuration, gets overwritten by the C# code's settings handler
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ASPNETCORE_URLS = "http://*:7801"

makensis.exe installer_script.nsi 

if ($LASTEXITCODE -ne 0) { 
    Read-Host -Prompt "Press Enter to continue..." 
}
