@echo off
set FXC_PATH=""

rem Search for fxc.exe in typical Windows SDK paths
for /d %%d in ("C:\Program Files (x86)\Windows Kits\10\bin\*") do (
    if exist "%%d\x64\fxc.exe" (
        set FXC_PATH="%%d\x64\fxc.exe"
    )
)

if %FXC_PATH% == "" (
    rem If not found in Windows Kits 10, check for fxc.exe in PATH
    where fxc.exe >nul 2>nul
    if %errorlevel% equ 0 (
        set FXC_PATH="fxc.exe"
    )
)

if %FXC_PATH% == "" (
    echo Error: fxc.exe not found in Windows Kits or environment PATH.
    echo Please install Windows SDK or add fxc.exe to your PATH.
    pause
    exit /b 1
)

echo Using compiler: %FXC_PATH%
echo Compiling Binjyo\Shaders\Effect.hlsl...
%FXC_PATH% Binjyo\Shaders\Effect.hlsl /T ps_3_0 /Fo Binjyo\Resources\Effect.ps

if %errorlevel% equ 0 (
    echo Shader compiled successfully.
) else (
    echo Error: Shader compilation failed.
    pause
)
