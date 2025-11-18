using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CarTechAssist.Application.Services;
using CarTechAssist.Contracts.Common;
using CarTechAssist.Contracts.Usuarios;
using CarTechAssist.Api.Attributes;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;
using System.Linq;

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
            // Primeiro tenta obter do header
            var tenantIdHeader = Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(tenantIdHeader) && int.TryParse(tenantIdHeader, out var tenantId) && tenantId > 0)
            {
                // Se encontrou no header, valida com o JWT se disponível
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

            // Se não encontrou no header, tenta obter do JWT
            if (User?.Identity?.IsAuthenticated == true)
            {
                var jwtTenantId = User.FindFirst("TenantId")?.Value;
                if (!string.IsNullOrEmpty(jwtTenantId) && int.TryParse(jwtTenantId, out var jwtTenant) && jwtTenant > 0)
                {
                    return jwtTenant;
                }
            }

            throw new UnauthorizedAccessException("TenantId não encontrado no header X-Tenant-Id ou no token JWT. Faça login primeiro ou forneça o header X-Tenant-Id.");
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
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<UsuariosController>>();
            
            try
            {
                logger.LogInformation("🔍 LISTAR USUARIOS - Iniciando. Tipo: {Tipo}, Ativo: {Ativo}, Page: {Page}, PageSize: {PageSize}", 
                    tipo, ativo, page, pageSize);
                
                const int maxPageSize = 100;
                if (pageSize > maxPageSize)
                    pageSize = maxPageSize;
                if (pageSize < 1)
                    pageSize = 20;
                if (page < 1)
                    page = 1;

                var tenantId = GetTenantId();
                logger.LogInformation("🔍 LISTAR USUARIOS - TenantId obtido: {TenantId}", tenantId);

                var result = await _usuariosService.ListarAsync(
                    tenantId, tipo, ativo, page, pageSize, ct);
                
                logger.LogInformation("✅ LISTAR USUARIOS - Sucesso. Total: {Total}, Items: {Count}", 
                    result?.Total ?? 0, result?.Items?.Count() ?? 0);
                
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogError(ex, "❌ LISTAR USUARIOS - Erro de autorização: {Message}", ex.Message);
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ LISTAR USUARIOS - Erro inesperado: {Message}, StackTrace: {StackTrace}", 
                    ex.Message, ex.StackTrace);
                return StatusCode(500, new { message = "Erro ao listar usuários. Tente novamente.", error = ex.Message });
            }
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<UsuarioDto>> Obter(int id, CancellationToken ct = default)
        {

            var result = await _usuariosService.ObterAsync(GetTenantId(), id, ct);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost]
        [AuthorizeRoles(3)] // Apenas Admin(3) pode criar usuários
        public async Task<ActionResult<UsuarioDto>> Criar(
            [FromBody] CriarUsuarioRequest? request,
            CancellationToken ct = default)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<UsuariosController>>();

            logger.LogWarning("🔍 CRIAR USUARIO - Autenticado: {IsAuthenticated}, User: {User}, Claims: {Claims}",
                User?.Identity?.IsAuthenticated,
                User?.Identity?.Name,
                string.Join(", ", User?.Claims?.Select(c => $"{c.Type}={c.Value}") ?? Array.Empty<string>()));
            
            try
            {

                Request.EnableBuffering();
                Request.Body.Position = 0;
                using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
                var bodyContent = await reader.ReadToEndAsync();
                Request.Body.Position = 0;
                
                logger.LogWarning("🔍 CRIAR USUARIO - Body recebido: {Body}", bodyContent);
                logger.LogWarning("🔍 CRIAR USUARIO - Request deserializado: Login={Login}, NomeCompleto={NomeCompleto}, Email='{Email}', Telefone='{Telefone}', TipoUsuarioId={TipoUsuarioId}, Senha={HasSenha}", 
                    request?.Login, request?.NomeCompleto, request?.Email ?? "NULL", request?.Telefone ?? "NULL", request?.TipoUsuarioId, !string.IsNullOrEmpty(request?.Senha));
                logger.LogWarning("🔍 CRIAR USUARIO - Headers: X-Tenant-Id={TenantId}, Authorization={HasAuth}",
                    Request.Headers["X-Tenant-Id"].FirstOrDefault(),
                    Request.Headers.ContainsKey("Authorization"));

                if (request == null)
                {
                    logger.LogError("❌ CRIAR USUARIO - Request é NULL! Body: {Body}", bodyContent);
                    return BadRequest(new { message = "Dados inválidos. Request não pode ser nulo.", body = bodyContent });
                }

                if (string.IsNullOrWhiteSpace(request.Login))
                {
                    return BadRequest(new { message = "Login é obrigatório." });
                }

                if (string.IsNullOrWhiteSpace(request.NomeCompleto))
                {
                    return BadRequest(new { message = "Nome completo é obrigatório." });
                }

                if (string.IsNullOrWhiteSpace(request.Senha))
                {
                    return BadRequest(new { message = "Senha é obrigatória." });
                }

                if (request.Senha.Length < 6)
                {
                    return BadRequest(new { message = "A senha deve ter no mínimo 6 caracteres." });
                }

                if (request.TipoUsuarioId != 2 && request.TipoUsuarioId != 3)
                {
                    logger.LogWarning("❌ CRIAR USUARIO - TipoUsuarioId inválido: {TipoUsuarioId}", request.TipoUsuarioId);
                    return BadRequest(new { message = "Apenas Agente(2) e Admin(3) podem ser criados aqui." });
                }

                var tenantId = GetTenantId();
                logger.LogWarning("🔍 CRIAR USUARIO - TenantId obtido: {TenantId}", tenantId);
                
                var result = await _usuariosService.CriarAsync(tenantId, request, ct);
                
                logger.LogWarning("✅ CRIAR USUARIO - Usuário criado com sucesso. UsuarioId: {UsuarioId}", result.UsuarioId);
                
                return CreatedAtAction(nameof(Obter), new { id = result.UsuarioId }, result);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogError("❌ CRIAR USUARIO - Erro de autorização: {Message}", ex.Message);
                return StatusCode(403, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError("❌ CRIAR USUARIO - Erro de operação: {Message}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ CRIAR USUARIO - Erro inesperado: {Message}, StackTrace: {StackTrace}", ex.Message, ex.StackTrace);
                return StatusCode(500, new { message = "Erro ao criar usuário. Tente novamente.", error = ex.Message });
            }
        }



        [HttpPost("registro-publico")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<ActionResult<UsuarioDto>> RegistroPublico(
            [FromBody] CriarUsuarioRequest? request,
            CancellationToken ct = default)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<UsuariosController>>();
            
            try
            {

                Request.EnableBuffering();
                Request.Body.Position = 0;
                using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
                var bodyContent = await reader.ReadToEndAsync();
                Request.Body.Position = 0;
                
                logger.LogWarning("🔍 REGISTRO PUBLICO - Body recebido: {Body}", bodyContent);
                logger.LogWarning("🔍 REGISTRO PUBLICO - Request deserializado: Login={Login}, Email={Email}, TipoUsuarioId={TipoUsuarioId}", 
                    request?.Login, request?.Email, request?.TipoUsuarioId);

                if (request == null)
                {
                    logger.LogError("❌ REGISTRO PUBLICO - Request é NULL! Body: {Body}", bodyContent);
                    return BadRequest(new { message = "Dados inválidos. Request não pode ser nulo.", body = bodyContent });
                }

                if (request.TipoUsuarioId != 1)
                {
                    return BadRequest(new { message = "Este endpoint é apenas para registro de clientes." });
                }

                if (string.IsNullOrWhiteSpace(request.Login))
                {
                    return BadRequest(new { message = "Login é obrigatório." });
                }

                if (string.IsNullOrWhiteSpace(request.NomeCompleto))
                {
                    return BadRequest(new { message = "Nome completo é obrigatório." });
                }

                if (string.IsNullOrWhiteSpace(request.Email))
                {
                    return BadRequest(new { message = "Email é obrigatório para clientes." });
                }

                if (string.IsNullOrWhiteSpace(request.Senha))
                {
                    return BadRequest(new { message = "Senha é obrigatória." });
                }

                if (request.Senha.Length < 6)
                {
                    return BadRequest(new { message = "A senha deve ter no mínimo 6 caracteres." });
                }

                var tenantId = 1;

                var result = await _usuariosService.CriarAsync(tenantId, request, ct);

                if (result == null || result.UsuarioId <= 0)
                {
                    return StatusCode(500, new { message = "Erro ao criar conta. Usuário não foi criado corretamente." });
                }
                
                return CreatedAtAction(nameof(Obter), new { id = result.UsuarioId }, result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message, error = ex.InnerException?.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erro ao criar conta. Tente novamente.", error = ex.Message, innerError = ex.InnerException?.Message });
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
        public async Task<ActionResult<UsuarioDto>> AlterarAtivacao(
            int id,
            [FromBody] AlterarAtivacaoRequest request,
            CancellationToken ct = default)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<UsuariosController>>();
            
            try
            {
                logger.LogInformation("🔍 ALTERAR ATIVACAO - Iniciando. UsuarioId: {UsuarioId}", id);
                logger.LogInformation("🔍 ALTERAR ATIVACAO - Request recebido: Ativo={Ativo}", request?.Ativo);
                logger.LogInformation("🔍 ALTERAR ATIVACAO - Headers: X-Tenant-Id={TenantId}, Authorization={HasAuth}",
                    Request.Headers["X-Tenant-Id"].FirstOrDefault(),
                    Request.Headers.ContainsKey("Authorization"));
                
                if (request == null)
                {
                    logger.LogError("❌ ALTERAR ATIVACAO - Request é NULL!");
                    return BadRequest(new { message = "Dados inválidos. Request não pode ser nulo." });
                }
                
                logger.LogInformation("🔍 ALTERAR ATIVACAO - UsuarioId: {UsuarioId}, Ativo: {Ativo}", id, request.Ativo);
                
                var tenantId = GetTenantId();
                logger.LogInformation("🔍 ALTERAR ATIVACAO - TenantId: {TenantId}", tenantId);
                
                var result = await _usuariosService.AlterarAtivacaoAsync(tenantId, id, request.Ativo, ct);
                
                logger.LogInformation("✅ ALTERAR ATIVACAO - Sucesso. UsuarioId: {UsuarioId}, Nome: {Nome}, Ativo: {Ativo}", 
                    result.UsuarioId, result.NomeCompleto, result.Ativo);
                
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError(ex, "❌ ALTERAR ATIVACAO - Usuário não encontrado ou erro de operação. UsuarioId: {UsuarioId}, Message: {Message}", 
                    id, ex.Message);
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogError(ex, "❌ ALTERAR ATIVACAO - Erro de autorização. UsuarioId: {UsuarioId}, Message: {Message}", 
                    id, ex.Message);
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ ALTERAR ATIVACAO - Erro inesperado. UsuarioId: {UsuarioId}, Message: {Message}, StackTrace: {StackTrace}", 
                    id, ex.Message, ex.StackTrace);
                return StatusCode(500, new { message = "Erro ao alterar ativação do usuário.", error = ex.Message });
            }
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


