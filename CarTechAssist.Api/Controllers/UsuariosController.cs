using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CarTechAssist.Application.Services;
using CarTechAssist.Contracts.Common;
using CarTechAssist.Contracts.Usuarios;
using CarTechAssist.Api.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace CarTechAssist.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsuariosController : ControllerBase
    {
        private readonly UsuariosService _usuariosService;

        public UsuariosController(UsuariosService usuariosService)
        {
            _usuariosService = usuariosService;
        }

      
        private int GetTenantId()
        {
            var tenantIdHeader = Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (string.IsNullOrEmpty(tenantIdHeader) || !int.TryParse(tenantIdHeader, out var tenantId) || tenantId <= 0)
            {
                throw new UnauthorizedAccessException("TenantId não encontrado ou inválido no header X-Tenant-Id.");
            }

            // Validar se corresponde ao JWT
            if (User?.Identity?.IsAuthenticated == true)
            {
                var jwtTenantId = User.FindFirst("TenantId")?.Value;
                if (!string.IsNullOrEmpty(jwtTenantId) && int.TryParse(jwtTenantId, out var jwtTenant))
                {
                    if (jwtTenant != tenantId)
                    {
                        throw new UnauthorizedAccessException("TenantId do header não corresponde ao token JWT.");
                    }
                }
            }

            return tenantId;
        }

        [HttpGet]
        [AuthorizeRoles(1, 2, 3)] // Cliente(1), Agente(2), Admin(3) podem listar usuários
        public async Task<ActionResult<PagedResult<UsuarioDto>>> Listar(
            [FromQuery] byte? tipo,
            [FromQuery] bool? ativo,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
           
            const int maxPageSize = 100;
            if (pageSize > maxPageSize)
                pageSize = maxPageSize;
            if (pageSize < 1)
                pageSize = 20;
            if (page < 1)
                page = 1;

            var result = await _usuariosService.ListarAsync(
                GetTenantId(), tipo, ativo, page, pageSize, ct);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<UsuarioDto>> Obter(int id, CancellationToken ct = default)
        {
            // CORREÇÃO CRÍTICA: Passar tenantId para validação de multi-tenancy
            var result = await _usuariosService.ObterAsync(GetTenantId(), id, ct);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost]
        [AuthorizeRoles(3)] // Apenas Admin(3) pode criar usuários
        public async Task<ActionResult<UsuarioDto>> Criar(
            [FromBody] CriarUsuarioRequest request,
            CancellationToken ct = default)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<UsuariosController>>();
            
            try
            {
                logger.LogInformation("🔴 CONTROLLER: Recebida requisição para criar usuário. Login: {Login}, TipoUsuarioId: {TipoUsuarioId}",
                    request.Login, request.TipoUsuarioId);
                
                var tenantId = GetTenantId();
                logger.LogInformation("🔴 CONTROLLER: TenantId obtido: {TenantId}", tenantId);
                
                // Validar campos obrigatórios
                if (string.IsNullOrWhiteSpace(request.Login))
                {
                    logger.LogWarning("⚠️ Login vazio ou nulo");
                    return BadRequest(new { message = "Login é obrigatório." });
                }
                
                if (string.IsNullOrWhiteSpace(request.NomeCompleto))
                {
                    logger.LogWarning("⚠️ NomeCompleto vazio ou nulo");
                    return BadRequest(new { message = "Nome completo é obrigatório." });
                }
                
                if (string.IsNullOrWhiteSpace(request.Senha))
                {
                    logger.LogWarning("⚠️ Senha vazia ou nula");
                    return BadRequest(new { message = "Senha é obrigatória." });
                }
                
                if (request.Senha.Length < 6)
                {
                    logger.LogWarning("⚠️ Senha muito curta: {Length} caracteres", request.Senha.Length);
                    return BadRequest(new { message = "A senha deve ter no mínimo 6 caracteres." });
                }
                
                // Validar que é Agente(2) ou Admin(3)
                if (request.TipoUsuarioId != 2 && request.TipoUsuarioId != 3)
                {
                    logger.LogWarning("⚠️ TipoUsuarioId inválido: {TipoUsuarioId}. Esperado: 2 ou 3", request.TipoUsuarioId);
                    return BadRequest(new { message = "Apenas Agente(2) e Admin(3) podem ser criados aqui." });
                }
                
                logger.LogInformation("🔴 CONTROLLER: Validações passadas. Chamando service...");
                var result = await _usuariosService.CriarAsync(tenantId, request, ct);
                
                logger.LogInformation("🎉 CONTROLLER: Usuário criado com sucesso! UsuarioId: {UsuarioId}, Login: {Login}", 
                    result.UsuarioId, result.Login);
                
                return CreatedAtAction(nameof(Obter), new { id = result.UsuarioId }, result);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError(ex, "Erro ao criar usuário: {Message}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogError(ex, "Erro de autorização ao criar usuário: {Message}", ex.Message);
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro inesperado ao criar usuário: {Message}, StackTrace: {StackTrace}", 
                    ex.Message, ex.StackTrace);
                return StatusCode(500, new { message = "Erro ao criar usuário. Tente novamente." });
            }
        }

        /// <summary>
        /// Endpoint público para registro de clientes (sem autenticação)
        /// </summary>
        [HttpPost("registro-publico")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<ActionResult<UsuarioDto>> RegistroPublico(
            [FromBody] CriarUsuarioRequest request,
            CancellationToken ct = default)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<UsuariosController>>();
            
            try
            {
                logger.LogInformation("🔴 CONTROLLER (PUBLICO): Recebida requisição de registro público. Login: {Login}, Email: {Email}, TipoUsuarioId: {TipoUsuarioId}", 
                    request.Login, request.Email, request.TipoUsuarioId);
                
                // Validar que é apenas para clientes
                if (request.TipoUsuarioId != 1)
                {
                    logger.LogWarning("⚠️ Tentativa de criar usuário não-cliente via endpoint público. TipoUsuarioId: {TipoUsuarioId}", 
                        request.TipoUsuarioId);
                    return BadRequest(new { message = "Este endpoint é apenas para registro de clientes." });
                }

                // Validar campos obrigatórios
                if (string.IsNullOrWhiteSpace(request.Login))
                {
                    logger.LogWarning("⚠️ Login vazio ou nulo");
                    return BadRequest(new { message = "Login é obrigatório." });
                }
                
                if (string.IsNullOrWhiteSpace(request.NomeCompleto))
                {
                    logger.LogWarning("⚠️ NomeCompleto vazio ou nulo");
                    return BadRequest(new { message = "Nome completo é obrigatório." });
                }
                
                if (string.IsNullOrWhiteSpace(request.Senha))
                {
                    logger.LogWarning("⚠️ Senha vazia ou nula");
                    return BadRequest(new { message = "Senha é obrigatória." });
                }
                
                if (request.Senha.Length < 6)
                {
                    logger.LogWarning("⚠️ Senha muito curta: {Length} caracteres", request.Senha.Length);
                    return BadRequest(new { message = "A senha deve ter no mínimo 6 caracteres." });
                }

                // TenantId padrão para registro público
                var tenantId = 1;

                logger.LogInformation("🔴 CONTROLLER (PUBLICO): Validações passadas. TenantId: {TenantId}, Login: {Login}. Chamando service...", 
                    tenantId, request.Login);
                
                var result = await _usuariosService.CriarAsync(tenantId, request, ct);
                
                logger.LogInformation("🎉 CONTROLLER (PUBLICO): Cliente criado com sucesso! UsuarioId: {UsuarioId}, Login: {Login}", 
                    result.UsuarioId, result.Login);
                
                return CreatedAtAction(nameof(Obter), new { id = result.UsuarioId }, result);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError(ex, "Erro de validação ao criar cliente: {Message}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro inesperado ao criar cliente: {Message}, StackTrace: {StackTrace}", 
                    ex.Message, ex.StackTrace);
                return StatusCode(500, new { message = "Erro ao criar conta. Tente novamente." });
            }
        }

        [HttpPut("{id:int}")]
        [AuthorizeRoles(3)] // Apenas Admin(3) pode atualizar usuários
        public async Task<ActionResult<UsuarioDto>> Atualizar(
            int id,
            [FromBody] AtualizarUsuarioRequest request,
            CancellationToken ct = default)
        {
            try
            {
                var result = await _usuariosService.AtualizarAsync(GetTenantId(), id, request, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPatch("{id:int}/ativacao")]
        [AuthorizeRoles(3)] // Apenas Admin(3) pode ativar/desativar usuários
        public async Task<IActionResult> AlterarAtivacao(
            int id,
            [FromBody] AlterarAtivacaoRequest request,
            CancellationToken ct = default)
        {
            await _usuariosService.AlterarAtivacaoAsync(id, request.Ativo, ct);
            return Ok();
        }

        [HttpPost("{id:int}/reset-senha")]
        [AuthorizeRoles(3)] // Apenas Admin(3) pode resetar senhas
        public async Task<IActionResult> ResetSenha(
            int id,
            [FromBody] ResetSenhaRequest request,
            CancellationToken ct = default)
        {
            await _usuariosService.ResetSenhaAsync(id, request.NovaSenha, ct);
            return Ok();
        }
    }
}


