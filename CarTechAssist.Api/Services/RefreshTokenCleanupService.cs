using CarTechAssist.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CarTechAssist.Api.Services
{
  
    public class RefreshTokenCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RefreshTokenCleanupService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24); // Executar diariamente

        public RefreshTokenCleanupService(
            IServiceProvider serviceProvider,
            ILogger<RefreshTokenCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("RefreshTokenCleanupService iniciado. Executando limpeza a cada {Interval} horas", _checkInterval.TotalHours);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
                    
                    _logger.LogInformation("Iniciando limpeza de refresh tokens expirados...");
                    await repository.LimparExpiradosAsync(stoppingToken);
                    _logger.LogInformation("Limpeza de refresh tokens expirados concluída com sucesso");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao limpar refresh tokens expirados");
                    // Continuar mesmo em caso de erro para não parar o serviço
                }

                // Aguardar até a próxima execução
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("RefreshTokenCleanupService está sendo parado");
            await base.StopAsync(cancellationToken);
        }
    }
}

