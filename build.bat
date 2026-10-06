@echo off
chcp 65001 >nul
cd /d "%~dp0"

rem O estudio aberto trava o exe e o dotnet build falha na copia
tasklist /fi "imagename eq GordoKore.Studio.exe" | find /i "GordoKore.Studio.exe" >nul && (
    echo O GordoKore Studio está aberto: feche-o antes de compilar.
    pause
    exit /b 1
)

dotnet build src\GordoKore.Studio\GordoKore.Studio.csproj -c Release
if errorlevel 1 (
    echo.
    echo Falhou: veja as mensagens acima.
    pause
    exit /b 1
)

echo.
echo Pronto: src\GordoKore.Studio\bin\Release\net10.0-windows\GordoKore.Studio.exe
pause
