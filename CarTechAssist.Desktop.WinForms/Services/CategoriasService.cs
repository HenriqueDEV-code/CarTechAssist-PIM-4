using CarTechAssist.Contracts.Tickets;

namespace CarTechAssist.Desktop.WinForms.Services
{
    public class CategoriasService
    {
        private readonly ApiClientService _apiClient;

        public CategoriasService(ApiClientService apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task<List<CategoriaDto>?> ListarAsync(CancellationToken ct = default)
        {
            return await _apiClient.GetAsync<List<CategoriaDto>>("api/categorias", ct);
        }

        public async Task<CategoriaDto?> ObterAsync(int id, CancellationToken ct = default)
        {
            return await _apiClient.GetAsync<CategoriaDto>($"api/categorias/{id}", ct);
        }
    }
}

