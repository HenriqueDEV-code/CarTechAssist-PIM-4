using CarTechAssist.Contracts.Tickets;

namespace CarTechAssist.Web.Services
{
    public class CategoriasService
    {
        private readonly ApiClientService _apiClient;

        public CategoriasService(ApiClientService apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task<IReadOnlyList<CategoriaDto>?> ListarAsync(CancellationToken ct = default)
        {
            return await _apiClient.GetAsync<IReadOnlyList<CategoriaDto>>("api/categorias", ct);
        }
    }
}

