using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace CarTechAssist.Api.Hubs
{



    [Authorize]
    public class ChamadoHub : Hub
    {
        private readonly ILogger<ChamadoHub> _logger;

        public ChamadoHub(ILogger<ChamadoHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var tenantId = Context.User?.FindFirst("TenantId")?.Value;
            
            _logger.LogInformation("Cliente conectado ao Hub. ConnectionId: {ConnectionId}, UserId: {UserId}, TenantId: {TenantId}",
                Context.ConnectionId, userId, tenantId);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation("Cliente desconectado do Hub. ConnectionId: {ConnectionId}, UserId: {UserId}",
                Context.ConnectionId, userId);

            await base.OnDisconnectedAsync(exception);
        }



        public async Task EntrarChamado(long chamadoId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var tenantId = Context.User?.FindFirst("TenantId")?.Value;
            var grupo = $"chamado_{tenantId}_{chamadoId}";

            await Groups.AddToGroupAsync(Context.ConnectionId, grupo);
            
            _logger.LogInformation("Usuário {UserId} entrou no grupo do chamado {ChamadoId}. Grupo: {Grupo}",
                userId, chamadoId, grupo);

            await Clients.OthersInGroup(grupo).SendAsync("UsuarioConectado", new
            {
                UsuarioId = userId,
                ChamadoId = chamadoId
            });
        }



        public async Task SairChamado(long chamadoId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var tenantId = Context.User?.FindFirst("TenantId")?.Value;
            var grupo = $"chamado_{tenantId}_{chamadoId}";

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, grupo);
            
            _logger.LogInformation("Usuário {UserId} saiu do grupo do chamado {ChamadoId}",
                userId, chamadoId);

            await Clients.OthersInGroup(grupo).SendAsync("UsuarioDesconectado", new
            {
                UsuarioId = userId,
                ChamadoId = chamadoId
            });
        }



        public async Task EnviarMensagem(long chamadoId, string mensagem)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var nomeUsuario = Context.User?.FindFirst("NomeCompleto")?.Value ?? "Usuário";
            var tenantId = Context.User?.FindFirst("TenantId")?.Value;
            var grupo = $"chamado_{tenantId}_{chamadoId}";

            _logger.LogInformation("Mensagem enviada no chat. ChamadoId: {ChamadoId}, UsuarioId: {UsuarioId}, Grupo: {Grupo}",
                chamadoId, userId, grupo);

            await Clients.Group(grupo).SendAsync("NovaMensagem", new
            {
                ChamadoId = chamadoId,
                UsuarioId = int.Parse(userId ?? "0"),
                NomeUsuario = nomeUsuario,
                Mensagem = mensagem,
                DataEnvio = DateTime.UtcNow
            });
        }



        public async Task UsuarioDigitando(long chamadoId, bool digitando)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var nomeUsuario = Context.User?.FindFirst("NomeCompleto")?.Value ?? "Usuário";
            var tenantId = Context.User?.FindFirst("TenantId")?.Value;
            var grupo = $"chamado_{tenantId}_{chamadoId}";

            await Clients.OthersInGroup(grupo).SendAsync("UsuarioDigitando", new
            {
                ChamadoId = chamadoId,
                UsuarioId = int.Parse(userId ?? "0"),
                NomeUsuario = nomeUsuario,
                Digitando = digitando
            });
        }
    }
}

