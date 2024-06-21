# NSIS script to create an installer. command:  makensis.exe /LAUNCH .\installer_script.nsi

# Include Modern UI
!include "MUI2.nsh"

# Compression method, '/SOLID lzma' takes least space
# setCompressor /SOLID lzma

!define MUI_ICON "src\wwwroot\favicon.ico"
!define MUI_UNICON "src\wwwroot\favicon.ico"
!define UNINSTALLER "uninstaller.exe"
!define REG_UNINSTALL "Software\Microsoft\Windows\CurrentVersion\Uninstall\StableSwarmUI"

# Define variables
Name "StableSwarmUI"
OutFile "StableSwarmUI-Installer.exe"
# Var FilePath

# Default installation directory
InstallDir "$PROFILE\StableSwarmUI"

# Request application privileges for Windows Vista and later
RequestExecutionLevel admin

# Pages
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "LICENSE.txt"
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_WELCOME
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "English" # The first language is the default language

# Sections
Section "StableSwarmUI (required)"
    SetOutPath "$INSTDIR"
    # Add files to be installed
    File /r /x *.bat /x *.sh /x *.ps1 /x StableSwarmUI-Installer.exe /x DOCKERFILE /x .dockerignore \
    /x docker-compose.yml /x colab /x .github /x .git /x bin ${__FILEDIR__}\*.*

    # Register with Windows Installer (Control Panel Add/Remove Programs)

    WriteRegStr HKLM64 "${REG_UNINSTALL}" "DisplayName" "StableSwarmUI"
    # sets the uninstall string
    WriteRegStr HKLM64 "${REG_UNINSTALL}" "UninstallString" "$\"$INSTDIR\${UNINSTALLER}$\""
    WriteRegStr HKLM64 "${REG_UNINSTALL}" "InstallLocation" "$INSTDIR"
    WriteRegStr HKLM64 "${REG_UNINSTALL}" "DisplayIcon" "$INSTDIR\src\wwwroot\favicon.ico"
    # Name of the publisher in add or remove programs in control panel TODO: change to proper name
    WriteRegStr HKLM64 "${REG_UNINSTALL}" "Publisher" "Nirmal Senthilkumar"
    # Link to the github TODO: change to new github
    WriteRegStr HKLM64 "${REG_UNINSTALL}" "HelpLink" "https://github.com/nirmie/StableSwarmUI-EXE"
    # version
    WriteRegStr HKLM64 "${REG_UNINSTALL}" "DisplayVersion" "1.0.0"

    WriteUninstaller "$INSTDIR\${UNINSTALLER}"
SectionEnd

# Create Start Menu shortcut
Section "Start Menu Shortcut"
    CreateDirectory "$SMPROGRAMS\StableSwarmUI"
    CreateShortCut "$SMPROGRAMS\StableSwarmUI\StableSwarmUI.lnk" "$INSTDIR\StableSwarmUI.exe" "" "" 0
SectionEnd

# Uninstaller
Section "Uninstall"
    # Delete the installed files
    Delete "$INSTDIR\*.*"
    # Remove uninstaller
    Delete "$INSTDIR\${UNINSTALLER}"
    # Remove Start Menu shortcut
    Delete "$SMPROGRAMS\StableSwarmUI\StableSwarmUI.lnk"
    # Remove installation directory
    RMDir /r "$INSTDIR\"
    # Remove Start Menu directory if it's empty
    RMDir "$SMPROGRAMS\StableSwarmUI"
    # Remove registry keys
    DeleteRegKey HKLM64 "${REG_UNINSTALL}"
SectionEnd