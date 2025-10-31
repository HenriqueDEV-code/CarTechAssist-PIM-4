using CarTechAssist.Contracts.Tickets;
using CarTechAssist.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CarTechAssist.Application.Services
{
    public class CategoriasService
    {
        private readonly ICategoriasRepository _categoriasRepository;
        private readonly ILogger<CategoriasService> _logger;

        public CategoriasService(ICategoriasRepository categoriasRepository, ILogger<CategoriasService> logger)
        {
            _categoriasRepository = categoriasRepository;
            _logger = logger;
        }

        public async Task<IReadOnlyList<CategoriaDto>> ListarAtivasAsync(int tenantId, CancellationToken ct)
        {
            _logger.LogInformation("Listando categorias ativas para tenant {TenantId}", tenantId);
            
            var categorias = await _categoriasRepository.ListarAtivasAsync(tenantId, ct);

            return categorias.Select(c => new CategoriaDto(
                c.CategoriaId,
                c.Nome,
                c.Codigo,
                c.CategoriaPaiId
            )).ToList();
        }
    }
}

