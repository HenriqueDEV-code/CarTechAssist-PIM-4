using Microsoft.AspNetCore.Mvc;
using CarTechAssist.Application.Services;
using CarTechAssist.Contracts.Usuarios;
using CarTechAssist.Domain.Interfaces;

namespace CarTechAssist.Api.Controllers
{
    /// <summary>
    /// Controller temporário para configuração inicial do sistema.
    /// Permite criar o primeiro usuário admin sem autenticação.
    /// REMOVER EM PRODUÇÃO ou proteger com validação adicional.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SetupController : ControllerBase
    {
        private readonly UsuariosService _usuariosService;
        private readonly IUsuariosRepository _usuariosRepository;
        private readonly ILogger<SetupController> _logger;

        public SetupController(
            UsuariosService usuariosService, 
            IUsuariosRepository usuariosRepository,
            ILogger<SetupController> logger)
        {
            _usuariosService = usuariosService;
            _usuariosRepository = usuariosRepository;
            _logger = logger;
        }

        /// <summary>
        /// Cria o primeiro usuário admin do sistema.
        /// Este endpoint deve ser removido ou protegido em produção.
        /// </summary>
        [HttpPost("criar-admin")]
        public async Task<IActionResult> CriarAdmin(
            [FromBody] CriarAdminRequest request,
            CancellationToken ct = default)
        {
            try
            {
                // Verificar se já existe algum admin
                // Por segurança, vamos verificar se já existe usuário ativo
                var tenantId = request.TenantId > 0 ? request.TenantId : 1; // Default tenant 1

                var criarRequest = new CriarUsuarioRequest(
                    request.Login ?? "admin",
                    request.NomeCompleto ?? "Administrador",
                    request.Email,
                    request.Telefone,
                    3, // Administrador
                    request.Senha ?? "Admin@123"
                );

                var result = await _usuariosService.CriarAsync(tenantId, criarRequest, ct);

                _logger.LogWarning("⚠️ ATENÇÃO: Usuário admin criado via endpoint de setup. Remova este endpoint em produção!");

                return Ok(new
                {
                    message = "Usuário admin criado com sucesso!",
                    usuario = result,
                    credenciais = new
                    {
                        login = result.Login,
                        senha = "(a senha que você informou)"
                    }
                });
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("já está em uso"))
                {
                    return Conflict(new { message = ex.Message, hint = "O usuário já existe. Use o endpoint de reset de senha para definir uma senha." });
                }
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar usuário admin");
                return StatusCode(500, new { message = "Erro ao criar usuário admin.", error = ex.Message });
            }
        }

        /// <summary>
        /// Define senha para um usuário existente pelo login (para usuários criados via SQL sem senha).
        /// </summary>
        [HttpPost("definir-senha-admin")]
        public async Task<IActionResult> DefinirSenhaAdmin(
            [FromBody] DefinirSenhaRequest request,
            CancellationToken ct = default)
        {
            try
            {
                var tenantId = request.TenantId > 0 ? request.TenantId : 1;
                var login = request.Login ?? "admin";
                var novaSenha = request.NovaSenha ?? "Admin@123";

                // Buscar usuário pelo login
                var usuario = await _usuariosRepository.ObterPorLoginAsync(tenantId, login, ct);
                if (usuario == null)
                {
                    return NotFound(new { 
                        message = $"Usuário com login '{login}' não encontrado no tenant {tenantId}.",
                        hint = "Verifique se o usuário foi criado corretamente no banco de dados."
                    });
                }

                // Resetar senha usando o service
                await _usuariosService.ResetSenhaPorLoginAsync(tenantId, login, novaSenha, ct);

                _logger.LogWarning("⚠️ ATENÇÃO: Senha do usuário admin foi redefinida via endpoint de setup. Remova este endpoint em produção!");

                return Ok(new
                {
                    message = $"Senha do usuário '{login}' definida com sucesso!",
                    usuario = new
                    {
                        usuarioId = usuario.UsuarioId,
                        login = usuario.Login,
                        nomeCompleto = usuario.NomeCompleto
                    },
                    credenciais = new
                    {
                        login = login,
                        senha = "(a senha que você informou)"
                    }
                });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao definir senha");
                return StatusCode(500, new { message = "Erro ao definir senha.", error = ex.Message });
            }
        }
    }

    public record CriarAdminRequest(
        int TenantId = 1,
        string? Login = null,
        string? NomeCompleto = null,
        string? Email = null,
        string? Telefone = null,
        string? Senha = null
    );

    public record DefinirSenhaRequest(
        int TenantId = 1,
        string Login = "admin",
        string NovaSenha = "Admin@123"
    );
}

