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

                if (string.IsNullOrWhiteSpace(request.Login))
                {
                    return BadRequest(new { 
                        message = "Login é obrigatório.",
                        success = false,
                        error = "LoginObrigatorio"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Email))
                {
                    return BadRequest(new { 
                        message = "Email é obrigatório.",
                        success = false,
                        error = "EmailObrigatorio"
                    });
                }

                var tenantId = 1;

                _logger.LogInformation("Solicitação de recuperação recebida. Login: {Login}, Email: {Email}", 
                    request.Login, request.Email);

                var resultado = await _recuperacaoSenhaService.SolicitarRecuperacaoComDetalhesAsync(
                    tenantId,
                    request.Login.Trim(),
                    request.Email.Trim(),
                    ct);

                return Ok(new
                {
                    message = "Código de recuperação enviado com sucesso! Verifique sua caixa de entrada e spam.",
                    success = true,
                    emailEnviado = resultado.EmailEnviado,

                    codigo = resultado.EmailEnviado ? null : resultado.Codigo
                });
            }
            catch (ArgumentException ex)
            {

                _logger.LogWarning("Erro de validação na recuperação de senha: {Erro}", ex.Message);
                return BadRequest(new { 
                    message = ex.Message,
                    success = false,
                    error = "ValidacaoErro"
                });
            }
            catch (InvalidOperationException ex)
            {

                _logger.LogError("🚫🚫🚫 VALIDAÇÃO FALHOU NO CONTROLLER - Erro na recuperação de senha: {Erro}", ex.Message);
                _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);


                string mensagemSegura;
                if (ex.Message.Contains("email cadastrado para o login") || ex.Message.Contains("é:"))
                {

                    if (ex.Message.Contains("não corresponde"))
                    {
                        mensagemSegura = "O email informado não corresponde ao cadastrado.";
                    }
                    else if (ex.Message.Contains("não encontrado"))
                    {
                        mensagemSegura = "Usuário não encontrado.";
                    }
                    else
                    {
                        mensagemSegura = "Erro ao processar solicitação. Verifique os dados informados.";
                    }
                }
                else
                {

                    mensagemSegura = ex.Message;
                }

                return BadRequest(new { 
                    message = mensagemSegura,
                    success = false,
                    error = "EmailInvalido"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao solicitar recuperação de senha. Detalhes: {Erro}", ex.Message);

                return StatusCode(500, new { 
                    message = "Ocorreu um erro ao processar sua solicitação. Tente novamente mais tarde.",
                    success = false,
                    error = "ErroInterno"
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

