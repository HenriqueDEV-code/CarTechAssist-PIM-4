using CarTechAssist.Application.Services;
using CarTechAssist.Contracts.ChatBot;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CarTechAssist.Api.Attributes;

namespace CarTechAssist.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [AuthorizeRoles(1)] // Apenas Cliente(1) pode usar o ChatBot
    public class ChatBotController : ControllerBase
    {
        private readonly ChatBotService _chatBotService;
        private readonly ILogger<ChatBotController> _logger;

        public ChatBotController(
            ChatBotService chatBotService,
            ILogger<ChatBotController> logger)
        {
            _chatBotService = chatBotService;
            _logger = logger;
        }

        private int GetTenantId() => int.Parse(Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "1");
        private int GetUsuarioId() => int.Parse(Request.Headers["X-Usuario-Id"].FirstOrDefault() ?? "1");

        [HttpPost("mensagem")]
        public async Task<ActionResult<ChatBotResponse>> EnviarMensagem(
            [FromBody] ChatBotRequest request,
            CancellationToken ct = default)
        {
            try
            {
                // Validações
                if (request == null)
                {
                    _logger.LogWarning("Request nulo recebido no ChatBot");
                    return BadRequest(new { message = "Requisição inválida. Os dados não foram fornecidos." });
                }

                if (string.IsNullOrWhiteSpace(request.Mensagem))
                {
                    _logger.LogWarning("Mensagem vazia recebida no ChatBot");
                    return BadRequest(new { message = "A mensagem não pode estar vazia." });
                }

                if (request.Mensagem.Length > 5000)
                {
                    _logger.LogWarning("Mensagem muito longa recebida: {Length} caracteres", request.Mensagem.Length);
                    return BadRequest(new { message = "A mensagem é muito longa. Limite máximo: 5000 caracteres." });
                }

                var tenantId = GetTenantId();
                var usuarioId = GetUsuarioId();

                _logger.LogInformation("Processando mensagem do ChatBot. TenantId: {TenantId}, UsuarioId: {UsuarioId}, ChamadoId: {ChamadoId}, MensagemLength: {MensagemLength}",
                    tenantId, usuarioId, request.ChamadoId, request.Mensagem.Length);

                var resposta = await _chatBotService.ProcessarMensagemAsync(
                    tenantId,
                    usuarioId,
                    request.Mensagem.Trim(),
                    request.ChamadoId,
                    ct);

                if (resposta == null)
                {
                    _logger.LogError("ChatBotService retornou resposta nula. TenantId: {TenantId}, UsuarioId: {UsuarioId}", tenantId, usuarioId);
                    return StatusCode(500, new { message = "Erro ao processar mensagem. O serviço não retornou resposta." });
                }

                _logger.LogInformation("ChatBot processou mensagem com sucesso. CriouChamado: {CriouChamado}, ChamadoId: {ChamadoId}",
                    resposta.CriouChamado, resposta.ChamadoId);

                return Ok(resposta);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Argumento inválido no ChatBot: {Message}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Acesso não autorizado no ChatBot: {Message}", ex.Message);
                return StatusCode(403, new { message = "Você não tem permissão para usar esta funcionalidade." });
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Operação cancelada (timeout) no ChatBot");
                return StatusCode(408, new { message = "A operação demorou muito para responder. Tente novamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar mensagem do ChatBot. Mensagem: {Message}, StackTrace: {StackTrace}", 
                    ex.Message, ex.StackTrace);
                return StatusCode(500, new { message = "Erro interno ao processar sua mensagem. Nossa equipe foi notificada." });
            }
        }

        [HttpGet("historico/{chamadoId:long}")]
        public async Task<ActionResult<IReadOnlyList<ChatBotMensagemDto>>> ObterHistorico(
            long chamadoId,
            CancellationToken ct = default)
        {
            try
            {
                // Obter interações do chamado (já existe endpoint para isso)
                // Por enquanto, retornar vazio - pode usar o endpoint de interações existente
                return Ok(new List<ChatBotMensagemDto>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao obter histórico do ChatBot");
                return StatusCode(500, new { message = "Erro ao obter histórico." });
            }
        }
    }
}

