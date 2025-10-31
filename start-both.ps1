# Script PowerShell para iniciar API e Web simultaneamente
# Uso: .\start-both.ps1

Write-Host "🚀 Iniciando CarTechAssist API e Web..." -ForegroundColor Green

# Caminhos dos projetos
$apiPath = "CarTechAssist.Api"
$webPath = "CarTechAssist.Web"

# Verificar se os projetos existem
if (-not (Test-Path $apiPath)) {
    Write-Host "❌ Projeto API não encontrado: $apiPath" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $webPath)) {
    Write-Host "❌ Projeto Web não encontrado: $webPath" -ForegroundColor Red
    exit 1
}

Write-Host "📦 Iniciando API..." -ForegroundColor Yellow
Start-Process dotnet -ArgumentList "run", "--project", $apiPath -WindowStyle Normal

# Aguardar um pouco para a API iniciar
Start-Sleep -Seconds 3

Write-Host "📦 Iniciando Web..." -ForegroundColor Yellow
Start-Process dotnet -ArgumentList "run", "--project", $webPath -WindowStyle Normal

Write-Host "✅ Ambos os projetos foram iniciados!" -ForegroundColor Green
Write-Host "🌐 API: https://localhost:7294/swagger" -ForegroundColor Cyan
Write-Host "🌐 Web: https://localhost:7045 ou http://localhost:5095" -ForegroundColor Cyan
Write-Host ""
Write-Host "Pressione qualquer tecla para encerrar os processos..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

