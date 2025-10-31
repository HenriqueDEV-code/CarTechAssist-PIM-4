using CarTechAssist.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CarTechAssist.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailTestController : ControllerBase
    {
        private readonly EmailService _emailService;
        private readonly ILogger<EmailTestController> _logger;

        public EmailTestController(EmailService emailService, ILogger<EmailTestController> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        [HttpPost("testar")]
        public async Task<ActionResult> TestarEmail(
            [FromBody] TestarEmailRequest request,
            CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("🧪 TESTE DE EMAIL - Enviando para: {Email}", request.EmailDestino);

                var resultado = await _emailService.EnviarEmailComDetalhesAsync(
                    request.EmailDestino,
                    "🧪 Teste CarTechAssist - Email Service",
                    @"
                    <h1>✅ Email de Teste</h1>
                    <p>Se você recebeu este email, o <strong>EmailService está funcionando corretamente!</strong></p>
                    <p>Data/Hora: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + @"</p>
                    <hr>
                    <p style='color: #666; font-size: 12px;'>
                        Este é um email de teste do sistema CarTechAssist.<br>
                        Se não solicitou este teste, ignore este email.
                    </p>",
                    true,
                    ct);

                if (resultado.Sucesso)
                {
                    return Ok(new 
                    { 
                        sucesso = true,
                        mensagem = "✅ Email enviado com sucesso! Verifique a caixa de entrada e spam.",
                        emailDestino = request.EmailDestino
                    });
                }
                else
                {
                    _logger.LogError("❌ Email não enviado. Erro detalhado: {Erro}", resultado.ErroDetalhado ?? "Sem detalhes");
                    
                    return StatusCode(500, new 
                    { 
                        sucesso = false,
                        mensagem = "❌ Falha ao enviar email.",
                        emailDestino = request.EmailDestino,
                        erro = resultado.ErroDetalhado ?? "Erro desconhecido - verifique logs da API",
                        hint = "Verifique o console da API onde está rodando para ver o erro específico completo."
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ERRO ao testar envio de email. Detalhes: {Erro}", ex.Message);
                _logger.LogError("❌ StackTrace completo: {StackTrace}", ex.StackTrace);
                if (ex.InnerException != null)
                {
                    _logger.LogError("❌ InnerException: {InnerException}", ex.InnerException.Message);
                }
                
                return StatusCode(500, new 
                { 
                    sucesso = false,
                    mensagem = $"Erro ao testar email: {ex.Message}",
                    erro = ex.Message,
                    tipoErro = ex.GetType().Name,
                    innerException = ex.InnerException?.Message,
                    hint = "Verifique os logs detalhados no console da API para mais informações sobre o erro SMTP."
                });
            }
        }
    }

    public record TestarEmailRequest(string EmailDestino);
}

