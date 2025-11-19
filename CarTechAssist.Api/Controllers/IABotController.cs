using CarTechAssist.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarTechAssist.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class IABotController : ControllerBase
    {
        private readonly IABotService _iaBotService;
        private readonly ILogger<IABotController> _logger;

        public IABotController(IABotService iaBotService, ILogger<IABotController> logger)
        {
            _iaBotService = iaBotService;
            _logger = logger;
        }

        /// <summary>
        /// Processa um chamado criado por um cliente usando IA.
        /// </summary>
        [HttpPost("processar-chamado/{chamadoId:long}")]
        public async Task<IActionResult> ProcessarChamado(long chamadoId, CancellationToken ct = default)
        {
            try
            {
                // Verificar se o usuário logado é Cliente (IA só processa quando cliente está logado)
                var tipoUsuarioId = GetTipoUsuarioId();
                if (tipoUsuarioId != 1) // 1 = Cliente
                {
                    _logger.LogInformation("⏸️ Usuário logado não é Cliente (TipoUsuarioId={TipoUsuarioId}). IA não processa quando Admin ou Agente está logado.", tipoUsuarioId);
                    return BadRequest(new { 
                        success = false, 
                        message = "A IA só processa chamados quando um cliente está logado. Agentes e administradores devem atender manualmente." 
                    });
                }

                var tenantId = GetTenantId();
                _logger.LogInformation("🤖 Processando chamado {ChamadoId} pelo Bot IA. TenantId: {TenantId}, TipoUsuarioId: {TipoUsuarioId}", chamadoId, tenantId, tipoUsuarioId);

                var resultado = await _iaBotService.ProcessarChamadoAsync(chamadoId, tenantId, ct);

                return Ok(new
                {
                    success = resultado.Sucesso,
                    message = resultado.Mensagem,
                    statusAtualizado = resultado.StatusAtualizado,
                    novoStatus = resultado.NovoStatus
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Erro ao processar chamado {ChamadoId}: {Message}", chamadoId, ex.Message);
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Erro de autorização ao processar chamado {ChamadoId}: {Message}", chamadoId, ex.Message);
                return StatusCode(403, new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException != null ? $" InnerException: {ex.InnerException.Message}" : "";
                _logger.LogError(ex, "❌ Erro inesperado ao processar chamado {ChamadoId}. Tipo: {Tipo}, Message: {Message}, InnerException: {InnerException}, StackTrace: {StackTrace}", 
                    chamadoId, ex.GetType().Name, ex.Message, ex.InnerException?.Message, ex.StackTrace);
                return StatusCode(500, new { 
                    success = false, 
                    message = $"Erro ao processar chamado com IA: {ex.Message}{innerMessage}",
                    errorType = ex.GetType().Name,
                    innerError = ex.InnerException?.Message
                });
            }
        }

        /// <summary>
        /// Processa uma nova mensagem do cliente em um chamado existente.
        /// </summary>
        [HttpPost("processar-mensagem/{chamadoId:long}")]
        public async Task<IActionResult> ProcessarMensagem(
            long chamadoId,
            [FromBody] ProcessarMensagemRequest request,
            CancellationToken ct = default)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Mensagem))
                {
                    return BadRequest(new { success = false, message = "Mensagem é obrigatória." });
                }

                // Verificar se o usuário logado é Cliente (IA só processa quando cliente está logado)
                var tipoUsuarioId = GetTipoUsuarioId();
                if (tipoUsuarioId != 1) // 1 = Cliente
                {
                    _logger.LogInformation("⏸️ Usuário logado não é Cliente (TipoUsuarioId={TipoUsuarioId}). IA não processa quando Admin ou Agente está logado.", tipoUsuarioId);
                    return BadRequest(new { 
                        success = false, 
                        message = "A IA só processa mensagens quando um cliente está logado. Agentes e administradores devem atender manualmente." 
                    });
                }

                var tenantId = GetTenantId();
                _logger.LogInformation("🤖 Processando mensagem do cliente no chamado {ChamadoId}. TenantId: {TenantId}, TipoUsuarioId: {TipoUsuarioId}", chamadoId, tenantId, tipoUsuarioId);

                var resultado = await _iaBotService.ProcessarMensagemClienteAsync(
                    chamadoId,
                    tenantId,
                    request.Mensagem,
                    ct);

                return Ok(new
                {
                    success = resultado.Sucesso,
                    message = resultado.Mensagem,
                    statusAtualizado = resultado.StatusAtualizado,
                    novoStatus = resultado.NovoStatus
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Erro ao processar mensagem no chamado {ChamadoId}: {Message}", chamadoId, ex.Message);
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Erro de autorização ao processar mensagem no chamado {ChamadoId}: {Message}", chamadoId, ex.Message);
                return StatusCode(403, new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao processar mensagem no chamado {ChamadoId}: {Message}", chamadoId, ex.Message);
                return StatusCode(500, new { success = false, message = "Erro ao processar mensagem com IA.", error = ex.Message });
            }
        }

        private int GetTenantId()
        {
            var tenantIdHeader = Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (int.TryParse(tenantIdHeader, out var tenantId))
            {
                return tenantId;
            }

            // Tentar obter do claim
            var tenantIdClaim = User?.Claims?.FirstOrDefault(c => c.Type == "TenantId");
            if (tenantIdClaim != null && int.TryParse(tenantIdClaim.Value, out tenantId))
            {
                return tenantId;
            }

            return 1; // Default
        }

        private byte GetTipoUsuarioId()
        {
            // Tentar obter do claim Role (onde o TipoUsuarioId é armazenado no JWT)
            var tipoUsuarioIdClaim = User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (!string.IsNullOrEmpty(tipoUsuarioIdClaim) && byte.TryParse(tipoUsuarioIdClaim, out var tipoUsuarioId))
            {
                return tipoUsuarioId;
            }

            // Tentar obter do header (fallback)
            var tipoUsuarioIdHeader = Request.Headers["X-Tipo-Usuario-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(tipoUsuarioIdHeader) && byte.TryParse(tipoUsuarioIdHeader, out tipoUsuarioId))
            {
                return tipoUsuarioId;
            }

            // Default: assumir Cliente se não encontrar
            return 1;
        }
    }

    public record ProcessarMensagemRequest(string Mensagem);
}

