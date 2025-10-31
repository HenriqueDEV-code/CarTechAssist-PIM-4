using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
            var tenantIdHeader = Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (string.IsNullOrEmpty(tenantIdHeader) || !int.TryParse(tenantIdHeader, out var tenantId))
                throw new UnauthorizedAccessException("TenantId não encontrado ou inválido no header X-Tenant-Id.");
            return tenantId;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<CategoriaDto>>> Listar(CancellationToken ct = default)
        {
            var result = await _categoriasService.ListarAtivasAsync(GetTenantId(), ct);
            return Ok(result);
        }
    }
}

