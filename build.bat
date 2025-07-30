@echo off
echo Building Jellyfin Audio Tagger Plugin...

REM Check if .NET SDK is installed
dotnet --version > nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo ERROR: .NET SDK not found. Please install .NET 8.0 SDK first.
    echo Download from: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

REM Build the plugin
echo Building plugin...
dotnet build --configuration Release

if %ERRORLEVEL% neq 0 (
    echo ERROR: Build failed!
    pause
    exit /b 1
)

REM Create release package
echo Creating release package...
set "OUTPUT_DIR=bin\Release\net8.0"
set "PACKAGE_DIR=jellyfin-plugin-audiotagger_1.0.0.0"

if exist "%PACKAGE_DIR%" rmdir /s /q "%PACKAGE_DIR%"
mkdir "%PACKAGE_DIR%"

REM Copy plugin files
copy "%OUTPUT_DIR%\Jellyfin.Plugin.AudioTagger.dll" "%PACKAGE_DIR%\"
copy "meta.json" "%PACKAGE_DIR%\"

REM Create zip package
if exist "%PACKAGE_DIR%.zip" del "%PACKAGE_DIR%.zip"
powershell Compress-Archive -Path "%PACKAGE_DIR%\*" -DestinationPath "%PACKAGE_DIR%.zip"

echo.
echo ======================================
echo Plugin built successfully!
echo.
echo Package: %PACKAGE_DIR%.zip
echo.
echo Installation:
echo 1. Extract to Jellyfin plugins directory
echo 2. Restart Jellyfin
echo 3. Configure in Dashboard ^> Plugins ^> Audio Tagger
echo ======================================
echo.

pause
