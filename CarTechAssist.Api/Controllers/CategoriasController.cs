using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using CarTechAssist.Application.Services;
using CarTechAssist.Contracts.Tickets;

namespace CarTechAssist.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CategoriasController : ControllerBase
    {
        private readonly CategoriasService _categoriasService;

        public CategoriasController(CategoriasService categoriasService)
        {
            _categoriasService = categoriasService;
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
        public async Task<ActionResult<IReadOnlyList<CategoriaDto>>> Listar(CancellationToken ct = default)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<CategoriasController>>();
            
            try
            {
                var tenantId = GetTenantId();
                logger.LogInformation("🔍 LISTAR CATEGORIAS - TenantId: {TenantId}", tenantId);
                
                var result = await _categoriasService.ListarAtivasAsync(tenantId, ct);
                
                logger.LogInformation("✅ LISTAR CATEGORIAS - Sucesso. Total: {Count}", result?.Count ?? 0);
                
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogError(ex, "❌ LISTAR CATEGORIAS - Erro de autorização: {Message}", ex.Message);
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ LISTAR CATEGORIAS - Erro inesperado: {Message}", ex.Message);
                return StatusCode(500, new { message = "Erro ao listar categorias. Tente novamente.", error = ex.Message });
            }
        }
    }
}

