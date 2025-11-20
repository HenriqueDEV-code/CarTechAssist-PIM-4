using CarTechAssist.Contracts.Common;
using CarTechAssist.Contracts.Tickets;
using CarTechAssist.Contracts.Enums;

namespace CarTechAssist.Desktop.WinForms.Services
{
    public class ChamadosService
    {
        private readonly ApiClientService _apiClient;

        public ChamadosService(ApiClientService apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task<PagedResult<TicketView>?> ListarAsync(
            byte? statusId = null,
            int? responsavelUsuarioId = null,
            int? solicitanteUsuarioId = null,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default)
        {
            var queryParams = new List<string>();
            if (statusId.HasValue) queryParams.Add($"statusId={statusId.Value}");
            if (responsavelUsuarioId.HasValue) queryParams.Add($"responsavelUsuarioId={responsavelUsuarioId.Value}");
            if (solicitanteUsuarioId.HasValue) queryParams.Add($"solicitanteUsuarioId={solicitanteUsuarioId.Value}");
            queryParams.Add($"page={page}");
            queryParams.Add($"pageSize={pageSize}");

            var endpoint = $"api/chamados?{string.Join("&", queryParams)}";
            return await _apiClient.GetAsync<PagedResult<TicketView>>(endpoint, ct);
        }

        public async Task<ChamadoDetailDto?> ObterAsync(long id, CancellationToken ct = default)
        {
            return await _apiClient.GetAsync<ChamadoDetailDto>($"api/chamados/{id}", ct);
        }

        public async Task<ChamadoDetailDto?> CriarAsync(CriarChamadoRequest request, CancellationToken ct = default)
        {
            return await _apiClient.PostAsync<ChamadoDetailDto>("api/chamados", request, ct);
        }

        public async Task<ChamadoDetailDto?> AtualizarAsync(long id, AtualizarChamadoRequest request, CancellationToken ct = default)
        {
            return await _apiClient.PutAsync<ChamadoDetailDto>($"api/chamados/{id}", request, ct);
        }

        public async Task<List<InteracaoDto>?> ListarInteracoesAsync(long chamadoId, CancellationToken ct = default)
        {
            return await _apiClient.GetAsync<List<InteracaoDto>>($"api/chamados/{chamadoId}/interacoes", ct);
        }

        public async Task<InteracaoDto?> AdicionarInteracaoAsync(long id, AdicionarInteracaoRequest request, CancellationToken ct = default)
        {
            return await _apiClient.PostAsync<InteracaoDto>($"api/chamados/{id}/interacoes", request, ct);
        }

        public async Task<object?> ProcessarChamadoComIAAsync(long id, CancellationToken ct = default)
        {
            return await _apiClient.PostAsync<object>($"api/iabot/processar-chamado/{id}", null, ct);
        }

        public async Task<object?> ProcessarMensagemComIAAsync(long id, string mensagem, CancellationToken ct = default)
        {
            return await _apiClient.PostAsync<object>($"api/iabot/processar-mensagem/{id}", new { Mensagem = mensagem }, ct);
        }

        public async Task<ChamadoDetailDto?> AlterarStatusAsync(long id, AlterarStatusRequest request, CancellationToken ct = default)
        {
            return await _apiClient.PatchAsync<ChamadoDetailDto>($"api/chamados/{id}/status", request, ct);
        }

        public async Task<ChamadoDetailDto?> AtribuirResponsavelAsync(long id, AtribuirResponsavelRequest request, CancellationToken ct = default)
        {
            return await _apiClient.PatchAsync<ChamadoDetailDto>($"api/chamados/{id}/responsavel", request, ct);
        }
    }
}

