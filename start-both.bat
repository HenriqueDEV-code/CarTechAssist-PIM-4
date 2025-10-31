@echo off
echo ========================================
echo Iniciando CarTechAssist API e Web
echo ========================================
echo.

echo [1/2] Iniciando API...
start "CarTechAssist API" cmd /k "cd CarTechAssist.Api && dotnet run"

timeout /t 5 /nobreak >nul

echo [2/2] Iniciando Web...
start "CarTechAssist Web" cmd /k "cd CarTechAssist.Web && dotnet run"

echo.
echo ========================================
echo Projetos iniciados!
echo API: https://localhost:7294/swagger
echo Web: https://localhost:7045
echo ========================================
echo.
echo Pressione qualquer tecla para fechar esta janela...
pause >nul

