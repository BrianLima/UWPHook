############################################################################################
#      NSIS Installation Script created by NSIS Quick Setup Script Generator v1.09.18
#               Entirely Edited with NullSoft Scriptable Installation System
#              by Vlasis K. Barkas aka Red Wine red_wine@freemail.gr Sep 2006
############################################################################################
#
#     Edited by Briano 🤔
#
############################################################################################

!define APP_NAME "UWPHook"
!define COMP_NAME "Briano"
!define WEB_SITE "https://briano.dev"
!define VERSION "2.13.00.00"
!define COPYRIGHT "Briano � 2020 2021 2022"
!define DESCRIPTION "The easy way to add UWP and XGP games to Steam"
!define LICENSE_TXT "C:\Users\Brian\Documents\GitHub\UWPHook\README.md"
!define INSTALLER_NAME "C:\Users\Brian\Documents\GitHub\UWPHook\UWPHook.exe"
!define MAIN_APP_EXE "UWPHook.exe"
!define INSTALL_TYPE "SetShellVarContext current"
!define REG_ROOT "HKCU"
!define REG_APP_PATH "Software\Microsoft\Windows\CurrentVersion\App Paths\${MAIN_APP_EXE}"
!define UNINSTALL_PATH "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"

!define REG_START_MENU "Start Menu Folder"

var SM_Folder

######################################################################

VIProductVersion  "${VERSION}"
VIAddVersionKey "ProductName"  "${APP_NAME}"
VIAddVersionKey "CompanyName"  "${COMP_NAME}"
VIAddVersionKey "LegalCopyright"  "${COPYRIGHT}"
VIAddVersionKey "FileDescription"  "${DESCRIPTION}"
VIAddVersionKey "FileVersion"  "${VERSION}"

######################################################################

SetCompressor ZLIB
Name "${APP_NAME}"
Caption "${APP_NAME}"
OutFile "${INSTALLER_NAME}"
BrandingText "${APP_NAME}"
XPStyle on
InstallDirRegKey "${REG_ROOT}" "${REG_APP_PATH}" ""
InstallDir "$APPDATA\Briano\UWPHook"

######################################################################

!include "MUI.nsh"

!define MUI_ABORTWARNING
!define MUI_UNABORTWARNING

!insertmacro MUI_PAGE_WELCOME

!ifdef LICENSE_TXT
!insertmacro MUI_PAGE_LICENSE "${LICENSE_TXT}"
!endif

!insertmacro MUI_PAGE_DIRECTORY

!ifdef REG_START_MENU
!define MUI_STARTMENUPAGE_DEFAULTFOLDER "UWPHook"
!define MUI_STARTMENUPAGE_REGISTRY_ROOT "${REG_ROOT}"
!define MUI_STARTMENUPAGE_REGISTRY_KEY "${UNINSTALL_PATH}"
!define MUI_STARTMENUPAGE_REGISTRY_VALUENAME "${REG_START_MENU}"
!insertmacro MUI_PAGE_STARTMENU Application $SM_Folder
!endif

!insertmacro MUI_PAGE_INSTFILES

!define MUI_FINISHPAGE_RUN "$INSTDIR\${MAIN_APP_EXE}"
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM

!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "English"

######################################################################

Section -MainProgram
${INSTALL_TYPE}
SetOverwrite ifnewer
SetOutPath "$INSTDIR"
File "C:\Users\Brian\Documents\GitHub\UWPHook\UWPHook\bin\Release\Crc32.NET.dll"
File "C:\Users\Brian\Documents\GitHub\UWPHook\UWPHook\bin\Release\MaterialDesignColors.dll"
File "C:\Users\Brian\Documents\GitHub\UWPHook\UWPHook\bin\Release\MaterialDesignThemes.Wpf.dll"
File "C:\Users\Brian\Documents\GitHub\UWPHook\UWPHook\bin\Release\Newtonsoft.Json.dll"
File "C:\Users\Brian\Documents\GitHub\UWPHook\UWPHook\bin\Release\Serilog.dll"
File "C:\Users\Brian\Documents\GitHub\UWPHook\UWPHook\bin\Release\Serilog.Sinks.Console.dll"
File "C:\Users\Brian\Documents\GitHub\UWPHook\UWPHook\bin\Release\Serilog.Sinks.File.dll"
File "C:\Users\Brian\Documents\GitHub\UWPHook\UWPHook\bin\Release\SharpSteam.dll"
File "C:\Users\Brian\Documents\GitHub\UWPHook\UWPHook\bin\Release\System.Management.Automation.dll"
File "C:\Users\Brian\Documents\GitHub\UWPHook\UWPHook\bin\Release\System.Net.Http.Formatting.dll"
File "C:\Users\Brian\Documents\GitHub\UWPHook\UWPHook\bin\Release\VDFParser.dll"
File "C:\Users\Brian\Documents\GitHub\UWPHook\UWPHook\bin\Release\UWPHook.exe"
File "C:\Users\Brian\Documents\GitHub\UWPHook\UWPHook\bin\Release\UWPHook.exe.config"

CreateDirectory "$INSTDIR\Resources"
SetOutPath "$INSTDIR\Resources"
File "C:\Users\Brian\Documents\GitHub\UWPHook\UWPHook\bin\Release\Resources\KnownApps.json"

SectionEnd

######################################################################

Section -Icons_Reg
SetOutPath "$INSTDIR"
WriteUninstaller "$INSTDIR\uninstall.exe"

!ifdef REG_START_MENU
!insertmacro MUI_STARTMENU_WRITE_BEGIN Application
CreateDirectory "$SMPROGRAMS\$SM_Folder"
CreateShortCut "$SMPROGRAMS\$SM_Folder\${APP_NAME}.lnk" "$INSTDIR\${MAIN_APP_EXE}"
CreateShortCut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${MAIN_APP_EXE}"
CreateShortCut "$SMPROGRAMS\$SM_Folder\Uninstall ${APP_NAME}.lnk" "$INSTDIR\uninstall.exe"

!ifdef WEB_SITE
WriteIniStr "$INSTDIR\${APP_NAME} website.url" "InternetShortcut" "URL" "${WEB_SITE}"
CreateShortCut "$SMPROGRAMS\$SM_Folder\${APP_NAME} Website.lnk" "$INSTDIR\${APP_NAME} website.url"
!endif
!insertmacro MUI_STARTMENU_WRITE_END
!endif

!ifndef REG_START_MENU
CreateDirectory "$SMPROGRAMS\UWPHook"
CreateShortCut "$SMPROGRAMS\UWPHook\${APP_NAME}.lnk" "$INSTDIR\${MAIN_APP_EXE}"
CreateShortCut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${MAIN_APP_EXE}"
CreateShortCut "$SMPROGRAMS\UWPHook\Uninstall ${APP_NAME}.lnk" "$INSTDIR\uninstall.exe"

!ifdef WEB_SITE
WriteIniStr "$INSTDIR\${APP_NAME} website.url" "InternetShortcut" "URL" "${WEB_SITE}"
CreateShortCut "$SMPROGRAMS\UWPHook\${APP_NAME} Website.lnk" "$INSTDIR\${APP_NAME} website.url"
!endif
!endif

WriteRegStr ${REG_ROOT} "${REG_APP_PATH}" "" "$INSTDIR\${MAIN_APP_EXE}"
WriteRegStr ${REG_ROOT} "${UNINSTALL_PATH}"  "DisplayName" "${APP_NAME}"
WriteRegStr ${REG_ROOT} "${UNINSTALL_PATH}"  "UninstallString" "$INSTDIR\uninstall.exe"
WriteRegStr ${REG_ROOT} "${UNINSTALL_PATH}"  "DisplayIcon" "$INSTDIR\${MAIN_APP_EXE}"
WriteRegStr ${REG_ROOT} "${UNINSTALL_PATH}"  "DisplayVersion" "${VERSION}"
WriteRegStr ${REG_ROOT} "${UNINSTALL_PATH}"  "Publisher" "${COMP_NAME}"

!ifdef WEB_SITE
WriteRegStr ${REG_ROOT} "${UNINSTALL_PATH}"  "URLInfoAbout" "${WEB_SITE}"
!endif
SectionEnd

######################################################################

Section Uninstall
${INSTALL_TYPE}
Delete "$INSTDIR\MaterialDesignColors.dll"
Delete "$INSTDIR\MaterialDesignThemes.Wpf.dll"
Delete "$INSTDIR\MaterialDesignThemes.Wpf.xml"
Delete "$INSTDIR\Microsoft.Management.Infrastructure.dll"
Delete "$INSTDIR\SharpSteam.dll"
Delete "$INSTDIR\System.Management.Automation.dll"
Delete "$INSTDIR\System.Management.Automation.xml"
Delete "$INSTDIR\UWPHook.exe"
Delete "$INSTDIR\UWPHook.exe.config"
Delete "$INSTDIR\VDFParser.dll"
Delete "$INSTDIR\Crc32.NET.dll"
Delete "$INSTDIR\Crc32.NET.xml"
Delete "$INSTDIR\uninstall.exe"
Delete "$INSTDIR\System.Net.Http.Formatting.dll"
Delete "$INSTDIR\Newtonsoft.Json.dll"
Delete "$INSTDIR\System.dll"
Delete "$INSTDIR\Resources\KnownApps.json"
!ifdef WEB_SITE
Delete "$INSTDIR\${APP_NAME} website.url"
!endif


RmDir /r "$APPDATA\Briano"
RmDir "$INSTDIR\Resources"
RmDir "$INSTDIR"

!ifdef REG_START_MENU
!insertmacro MUI_STARTMENU_GETFOLDER "Application" $SM_Folder
Delete "$SMPROGRAMS\$SM_Folder\${APP_NAME}.lnk"
Delete "$SMPROGRAMS\$SM_Folder\Uninstall ${APP_NAME}.lnk"
!ifdef WEB_SITE
Delete "$SMPROGRAMS\$SM_Folder\${APP_NAME} Website.lnk"
!endif
Delete "$DESKTOP\${APP_NAME}.lnk"

RmDir "$SMPROGRAMS\$SM_Folder"
!endif

!ifndef REG_START_MENU
Delete "$SMPROGRAMS\UWPHook\${APP_NAME}.lnk"
Delete "$SMPROGRAMS\UWPHook\Uninstall ${APP_NAME}.lnk"
!ifdef WEB_SITE
Delete "$SMPROGRAMS\UWPHook\${APP_NAME} Website.lnk"
!endif
Delete "$DESKTOP\${APP_NAME}.lnk"

RmDir "$SMPROGRAMS\UWPHook"
!endif

DeleteRegKey ${REG_ROOT} "${REG_APP_PATH}"
DeleteRegKey ${REG_ROOT} "${UNINSTALL_PATH}"
SectionEnd

######################################################################
