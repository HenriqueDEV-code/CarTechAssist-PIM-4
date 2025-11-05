using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CarTechAssist.Application.Services;
using CarTechAssist.Contracts.Common;
using CarTechAssist.Contracts.Usuarios;
using CarTechAssist.Api.Attributes;

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
            try
            {
                var result = await _usuariosService.CriarAsync(GetTenantId(), request, ct);
                return CreatedAtAction(nameof(Obter), new { id = result.UsuarioId }, result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
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


