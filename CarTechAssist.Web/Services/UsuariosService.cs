using CarTechAssist.Contracts.Common;
using CarTechAssist.Contracts.Usuarios;

namespace CarTechAssist.Web.Services
{
    public class UsuariosService
    {
        private readonly ApiClientService _apiClient;

        public UsuariosService(ApiClientService apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task<PagedResult<UsuarioDto>?> ListarAsync(
            byte? tipo = null,
            bool? ativo = null,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default)
        {
            var queryParams = new List<string>();
            if (tipo.HasValue) queryParams.Add($"tipo={tipo.Value}");
            if (ativo.HasValue) queryParams.Add($"ativo={ativo.Value}");
            queryParams.Add($"page={page}");
            queryParams.Add($"pageSize={pageSize}");

            var endpoint = $"api/usuarios?{string.Join("&", queryParams)}";
            return await _apiClient.GetAsync<PagedResult<UsuarioDto>>(endpoint, ct);
        }

        public async Task<UsuarioDto?> ObterAsync(int id, CancellationToken ct = default)
        {
            return await _apiClient.GetAsync<UsuarioDto>($"api/usuarios/{id}", ct);
        }

        public async Task<UsuarioDto?> CriarAsync(CriarUsuarioRequest request, CancellationToken ct = default)
        {
            return await _apiClient.PostAsync<UsuarioDto>("api/usuarios", request, ct);
        }

        /// <summary>
        /// Criar usuário usando endpoint público (sem autenticação) - apenas para registro de clientes
        /// </summary>
        public async Task<UsuarioDto?> CriarPublicoAsync(CriarUsuarioRequest request, CancellationToken ct = default)
        {
            // Usar método PostAsyncSemAuth para requisição sem autenticação
            return await _apiClient.PostAsyncSemAuth<UsuarioDto>("api/usuarios/registro-publico", request, ct);
        }

        public async Task<UsuarioDto?> AtualizarAsync(int id, AtualizarUsuarioRequest request, CancellationToken ct = default)
        {
            return await _apiClient.PutAsync<UsuarioDto>($"api/usuarios/{id}", request, ct);
        }

        public async Task<UsuarioDto?> AtivarDesativarAsync(int id, AlterarAtivacaoRequest request, CancellationToken ct = default)
        {
            return await _apiClient.PatchAsync<UsuarioDto>($"api/usuarios/{id}/ativacao", request, ct);
        }

        public async Task<bool> ResetarSenhaAsync(int id, ResetSenhaRequest request, CancellationToken ct = default)
        {
            return await _apiClient.PostAsync<object>($"api/usuarios/{id}/reset-senha", request, ct) != null;
        }
    }
}

