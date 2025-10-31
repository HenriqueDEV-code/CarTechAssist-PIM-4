using CarTechAssist.Application.Services;
using CarTechAssist.Contracts.Auth;
using Microsoft.AspNetCore.Mvc;

namespace CarTechAssist.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RecuperacaoSenhaController : ControllerBase
    {
        private readonly RecuperacaoSenhaService _recuperacaoSenhaService;
        private readonly ILogger<RecuperacaoSenhaController> _logger;

        public RecuperacaoSenhaController(
            RecuperacaoSenhaService recuperacaoSenhaService,
            ILogger<RecuperacaoSenhaController> logger)
        {
            _recuperacaoSenhaService = recuperacaoSenhaService;
            _logger = logger;
        }

        [HttpPost("solicitar")]
        public async Task<ActionResult> SolicitarRecuperacao(
            [FromBody] SolicitarRecuperacaoRequest request,
            CancellationToken ct = default)
        {
            try
            {
                // TenantId padrão é 1 para recuperação pública
                var tenantId = 1;

                _logger.LogInformation("Solicitação de recuperação recebida. Login: {Login}, Email: {Email}", 
                    request.Login, request.Email);

                var resultado = await _recuperacaoSenhaService.SolicitarRecuperacaoComDetalhesAsync(
                    tenantId,
                    request.Login,
                    request.Email,
                    ct);

                // Retornar resultado com informações de debug (temporário para desenvolvimento)
                var response = new
                {
                    message = "Se o usuário e e-mail estiverem corretos, você receberá um código de recuperação por e-mail.",
                    success = true,
                    // DEBUG: Mostrar código se email falhou (apenas em desenvolvimento)
                    codigo = resultado.EmailEnviado ? null : resultado.Codigo,
                    emailEnviado = resultado.EmailEnviado,
                    usuarioEncontrado = resultado.UsuarioEncontrado
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao solicitar recuperação de senha. Detalhes: {Erro}", ex.Message);
                // Por segurança, retorna sucesso mesmo em caso de erro
                return Ok(new { 
                    message = "Se o usuário e e-mail estiverem corretos, você receberá um código de recuperação por e-mail.",
                    success = true
                });
            }
        }

        [HttpPost("validar-codigo")]
        public async Task<ActionResult<bool>> ValidarCodigo(
            [FromBody] ValidarCodigoRequest request,
            CancellationToken ct = default)
        {
            var valido = await _recuperacaoSenhaService.ValidarCodigoAsync(request.Codigo, ct);
            return Ok(new { valido });
        }

        [HttpPost("redefinir")]
        public async Task<ActionResult> RedefinirSenha(
            [FromBody] RedefinirSenhaRequest request,
            CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("Tentativa de redefinição de senha. Código recebido: {Codigo}", request.Codigo);

                if (string.IsNullOrWhiteSpace(request.Codigo))
                {
                    _logger.LogWarning("Código vazio na solicitação de redefinição");
                    return BadRequest(new { message = "Código é obrigatório." });
                }

                if (string.IsNullOrWhiteSpace(request.NovaSenha) || request.NovaSenha.Length < 6)
                {
                    _logger.LogWarning("Senha inválida na solicitação de redefinição");
                    return BadRequest(new { message = "A senha deve ter no mínimo 6 caracteres." });
                }

                var sucesso = await _recuperacaoSenhaService.RedefinirSenhaAsync(
                    request.Codigo.Trim(),
                    request.NovaSenha,
                    ct);

                if (!sucesso)
                {
                    _logger.LogWarning("Falha ao redefinir senha. Código inválido ou expirado: {Codigo}", request.Codigo);
                    return BadRequest(new { message = "Código inválido ou expirado." });
                }

                _logger.LogInformation("Senha redefinida com sucesso usando código: {Codigo}", request.Codigo);
                return Ok(new { message = "Senha redefinida com sucesso!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao redefinir senha. Detalhes: {Erro}", ex.Message);
                return StatusCode(500, new { message = "Erro ao redefinir senha. Tente novamente." });
            }
        }
    }

    public record ValidarCodigoRequest(string Codigo);
}

