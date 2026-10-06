@echo off
chcp 65001 >nul
cd /d "%~dp0"

rem Versao de producao: um unico GordoKore.Studio.exe autocontido (leva o runtime do .NET, roda sem .NET instalado),
rem sem PDB e comprimido. Sem trimming: o WinForms nao suporta. Para o dia a dia, o build.bat continua.

rem O exe publicado aberto trava a copia
if exist publish\GordoKore.Studio.exe del /q publish\GordoKore.Studio.exe 2>nul
if exist publish\GordoKore.Studio.exe (
    echo publish\GordoKore.Studio.exe está aberto: feche-o antes de publicar.
    pause
    exit /b 1
)
if exist publish rmdir /s /q publish

dotnet publish src\GordoKore.Studio\GordoKore.Studio.csproj -c Release -r win-x64 --self-contained true -o publish ^
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true ^
    -p:DebugType=none -p:DebugSymbols=false -p:SatelliteResourceLanguages=pt-BR
if errorlevel 1 (
    echo.
    echo Falhou: veja as mensagens acima.
    pause
    exit /b 1
)

echo.
echo Pronto: publish\GordoKore.Studio.exe
echo Roda em qualquer Windows 10/11 64 bits sem .NET. Para compilar módulos ainda precisa do MinGW 32 bits e do CMake.
pause
