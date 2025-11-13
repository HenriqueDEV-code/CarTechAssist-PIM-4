using CarTechAssist.Contracts.Common;
using CarTechAssist.Contracts.Enums;
using CarTechAssist.Contracts.Feedback;
using CarTechAssist.Contracts.Tickets;
using System.Net.Http;
using Microsoft.AspNetCore.Http;

namespace CarTechAssist.Web.Services
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

        public async Task<bool> DeletarAsync(long id, DeletarChamadoRequest request, CancellationToken ct = default)
        {
            return await _apiClient.DeleteAsync($"api/chamados/{id}", request, ct);
        }

        public async Task<ChamadoDetailDto?> AlterarStatusAsync(long id, AlterarStatusRequest request, CancellationToken ct = default)
        {
            return await _apiClient.PatchAsync<ChamadoDetailDto>($"api/chamados/{id}/status", request, ct);
        }

        public async Task<ChamadoDetailDto?> AtribuirResponsavelAsync(long id, AtribuirResponsavelRequest request, CancellationToken ct = default)
        {
            return await _apiClient.PatchAsync<ChamadoDetailDto>($"api/chamados/{id}/responsavel", request, ct);
        }

        public async Task<IReadOnlyList<InteracaoDto>?> ListarInteracoesAsync(long id, CancellationToken ct = default)
        {
            return await _apiClient.GetAsync<IReadOnlyList<InteracaoDto>>($"api/chamados/{id}/interacoes", ct);
        }

        public async Task<InteracaoDto?> AdicionarInteracaoAsync(long id, AdicionarInteracaoRequest request, CancellationToken ct = default)
        {
            return await _apiClient.PostAsync<InteracaoDto>($"api/chamados/{id}/interacoes", request, ct);
        }

        public async Task<IReadOnlyList<AnexoDto>?> ListarAnexosAsync(long id, CancellationToken ct = default)
        {
            return await _apiClient.GetAsync<IReadOnlyList<AnexoDto>>($"api/chamados/{id}/anexos", ct);
        }

        public async Task<Stream> DownloadAnexoAsync(long id, long anexoId, CancellationToken ct = default)
        {
            return await _apiClient.GetStreamAsync($"api/chamados/{id}/anexos/{anexoId}", ct);
        }

        public async Task<object?> UploadAnexoAsync(long id, IFormFile arquivo, CancellationToken ct = default)
        {
            var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(arquivo.OpenReadStream());
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(arquivo.ContentType);
            content.Add(fileContent, "arquivo", arquivo.FileName);

            return await _apiClient.PostMultipartAsync<object>($"api/chamados/{id}/anexos", content, ct);
        }

        public async Task<IReadOnlyList<StatusHistoricoDto>?> ListarHistoricoStatusAsync(long id, CancellationToken ct = default)
        {
            return await _apiClient.GetAsync<IReadOnlyList<StatusHistoricoDto>>($"api/chamados/{id}/historico-status", ct);
        }

        public async Task<ChamadoDetailDto?> EnviarFeedbackAsync(long id, EnviarFeedbackRequest request, CancellationToken ct = default)
        {
            return await _apiClient.PostAsync<ChamadoDetailDto>($"api/chamados/{id}/feedback", request, ct);
        }

        public async Task<EstatisticasChamadosDto?> ObterEstatisticasAsync(CancellationToken ct = default)
        {
            return await _apiClient.GetAsync<EstatisticasChamadosDto>("api/chamados/estatisticas", ct);
        }
    }
}

