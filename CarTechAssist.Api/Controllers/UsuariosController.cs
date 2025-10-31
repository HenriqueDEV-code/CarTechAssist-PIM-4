using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CarTechAssist.Api.Attributes;
using CarTechAssist.Application.Services;
using CarTechAssist.Contracts.Common;
using CarTechAssist.Contracts.Usuarios;
using CarTechAssist.Domain.Enums;

namespace CarTechAssist.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
            if (string.IsNullOrEmpty(tenantIdHeader) || !int.TryParse(tenantIdHeader, out var tenantId))
                throw new UnauthorizedAccessException("TenantId não encontrado ou inválido no header X-Tenant-Id.");
            return tenantId;
        }

        [HttpGet]
        [Authorize] // Requer autenticação
        public async Task<ActionResult<PagedResult<UsuarioDto>>> Listar(
            [FromQuery] byte? tipo,
            [FromQuery] bool? ativo,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            // Validação de parâmetros de paginação
            if (page < 1)
                return BadRequest("O parâmetro 'page' deve ser maior ou igual a 1.");
            
            if (pageSize < 1 || pageSize > 100)
                return BadRequest("O parâmetro 'pageSize' deve estar entre 1 e 100.");

            var result = await _usuariosService.ListarAsync(
                GetTenantId(), tipo, ativo, page, pageSize, ct);
            return Ok(result);
        }

        [HttpGet("{id:int}")]
        [Authorize] // Requer autenticação
        public async Task<ActionResult<UsuarioDto>> Obter(int id, CancellationToken ct = default)
        {
            var result = await _usuariosService.ObterAsync(GetTenantId(), id, ct);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost]
        // Permite criar usuários sem autenticação apenas para registro de clientes
        // Se não for cliente, requer autenticação de admin
        public async Task<ActionResult<UsuarioDto>> Criar(
            [FromBody] CriarUsuarioRequest request,
            CancellationToken ct = default)
        {
            try
            {
                // Validar que apenas clientes podem se registrar sem autenticação
                // Se for outro tipo de usuário, requer autenticação de admin
                var isAuthenticated = User?.Identity?.IsAuthenticated == true;
                
                if (request.TipoUsuarioId != 1 && !isAuthenticated) // 1 = Cliente
                {
                    return Unauthorized("Apenas clientes podem se registrar publicamente. Para criar técnicos ou administradores, é necessário estar autenticado como administrador.");
                }

                // Se autenticado, verificar se é admin (apenas admins podem criar não-clientes)
                if (isAuthenticated && request.TipoUsuarioId != 1 && User != null)
                {
                    var roleClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Role);
                    if (roleClaim == null || !byte.TryParse(roleClaim.Value, out var userRole) || userRole != 3) // 3 = Administrador
                    {
                        return Forbid("Apenas administradores podem criar usuários técnicos ou outros administradores.");
                    }
                }

                // Tenta pegar TenantId do header, senão usa 1 como padrão
                var tenantIdHeader = Request.Headers["X-Tenant-Id"].FirstOrDefault();
                var tenantId = !string.IsNullOrEmpty(tenantIdHeader) && int.TryParse(tenantIdHeader, out var parsedTenantId)
                    ? parsedTenantId 
                    : 1;
            
                var result = await _usuariosService.CriarAsync(tenantId, request, ct);
                return CreatedAtAction(nameof(Obter), new { id = result.UsuarioId }, result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id:int}")]
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
        [AuthorizeRoles((byte)Domain.Enums.TipoUsuarios.Administrador, (byte)Domain.Enums.TipoUsuarios.Tecnico)] // Admin e Técnico podem ativar/desativar
        public async Task<IActionResult> AlterarAtivacao(
            int id,
            [FromBody] AlterarAtivacaoRequest request,
            CancellationToken ct = default)
        {
            try
            {
                await _usuariosService.AlterarAtivacaoAsync(GetTenantId(), id, request.Ativo, ct);
                return Ok();
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

        [HttpPost("{id:int}/reset-senha")]
        [AuthorizeRoles((byte)Domain.Enums.TipoUsuarios.Administrador, (byte)Domain.Enums.TipoUsuarios.Tecnico)] // Admin e Técnico podem resetar senhas
        public async Task<IActionResult> ResetSenha(
            int id,
            [FromBody] ResetSenhaRequest request,
            CancellationToken ct = default)
        {
            try
            {
                await _usuariosService.ResetSenhaAsync(GetTenantId(), id, request.NovaSenha, ct);
                return Ok(new { message = "Senha resetada com sucesso." });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}


