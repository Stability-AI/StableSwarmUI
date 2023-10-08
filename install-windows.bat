@echo off

echo Where do you want to install StableSwarmUI?
set /p installDir=Enter the installation path: 

echo You've chosen to install at: "%installDir%"

if not exist "%installDir%" (
    echo The specified directory does not exist. Please rerun this script
    pause
    exit
)

cd "%installDir%"


if exist StableSwarmUI (
    echo StableSwarmUI is already installed in this folder. If this is incorrect, delete the 'StableSwarmUI' folder and try again.
    pause
    exit
)

if exist StableSwarmUI.sln (
    echo StableSwarmUI is already installed in this folder. If this is incorrect, delete 'StableSwarmUI.sln' and try again.
    pause
    exit
)

winget install Microsoft.DotNet.SDK.7 --accept-source-agreements --accept-package-agreements
winget install --id Git.Git -e --source winget --accept-source-agreements --accept-package-agreements

git clone https://github.com/Stability-AI/StableSwarmUI
cd StableSwarmUI

.\make-shortcut.bat

.\launch-windows.bat --launch_mode webinstall

IF %ERRORLEVEL% NEQ 0 ( pause )
