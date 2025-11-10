using CarTechAssist.Contracts.Tickets;
using CarTechAssist.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarTechAssist.Web.Pages.Chamados
{
    public class DetalhesModel : PageModel
    {
        private readonly ChamadosService _chamadosService;
        private readonly ILogger<DetalhesModel> _logger;

        public ChamadoDetailDto? Chamado { get; set; }
        public IReadOnlyList<InteracaoDto>? Interacoes { get; set; }
        public IReadOnlyList<AnexoDto>? Anexos { get; set; }
        public string? ErrorMessage { get; set; }
        public int TenantId { get; set; }
        public int UsuarioId { get; set; }
        public byte TipoUsuarioId { get; set; }

        public DetalhesModel(ChamadosService chamadosService, ILogger<DetalhesModel> logger)
        {
            _chamadosService = chamadosService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync(long id, CancellationToken ct = default)
        {
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            var tenantIdStr = HttpContext.Session.GetString("TenantId");
            var usuarioIdStr = HttpContext.Session.GetString("UsuarioId");
            var tipoUsuarioIdStr = HttpContext.Session.GetString("TipoUsuarioId");

            if (!int.TryParse(tenantIdStr, out var tenantId))
                tenantId = 1;
            TenantId = tenantId;

            if (!int.TryParse(usuarioIdStr, out var usuarioId))
            {
                return RedirectToPage("/Login");
            }
            UsuarioId = usuarioId;

            if (!byte.TryParse(tipoUsuarioIdStr, out var tipoUsuarioId))
                tipoUsuarioId = 1;
            TipoUsuarioId = tipoUsuarioId;

            try
            {
                Chamado = await _chamadosService.ObterAsync(id, ct);
                if (Chamado == null)
                {
                    ErrorMessage = "Chamado não encontrado.";
                    return RedirectToPage("/Chamados");
                }

                Interacoes = await _chamadosService.ListarInteracoesAsync(id, ct);
                Anexos = await _chamadosService.ListarAnexosAsync(id, ct);
            }
            catch (System.Net.Http.HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Erro HTTP ao carregar detalhes do chamado {ChamadoId}", id);

                if (httpEx.Data.Contains("StatusCode") && httpEx.Data["StatusCode"] is System.Net.HttpStatusCode statusCode)
                {
                    if (statusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        return RedirectToPage("/Login");
                    }
                    else if (statusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        ErrorMessage = "Chamado não encontrado.";
                        return RedirectToPage("/Chamados");
                    }
                    else if (statusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        ErrorMessage = "Você não tem permissão para acessar este chamado.";
                    }
                    else
                    {
                        var apiMessage = httpEx.Data.Contains("Message") ? httpEx.Data["Message"]?.ToString() : null;
                        ErrorMessage = !string.IsNullOrEmpty(apiMessage) ? apiMessage : $"Erro ao carregar chamado: {httpEx.Message}";
                    }
                }
                else
                {
                    ErrorMessage = $"Erro ao carregar chamado: {httpEx.Message}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao carregar detalhes do chamado {ChamadoId}", id);
                ErrorMessage = "Erro ao carregar chamado. Tente novamente.";
            }

            return Page();
        }

        [Microsoft.AspNetCore.Mvc.ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAdicionarInteracaoAsync(long id, [FromForm] string mensagem, CancellationToken ct = default)
        {
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            if (string.IsNullOrWhiteSpace(mensagem))
            {
                return BadRequest("Mensagem não pode estar vazia.");
            }

            try
            {
                var request = new AdicionarInteracaoRequest(mensagem);
                await _chamadosService.AdicionarInteracaoAsync(id, request, ct);
                return RedirectToPage("/Chamados/Detalhes", new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao adicionar interação ao chamado {ChamadoId}", id);
                return BadRequest("Erro ao enviar mensagem. Tente novamente.");
            }
        }

        [Microsoft.AspNetCore.Mvc.ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostUploadAnexoAsync(long id, IFormFile arquivo, CancellationToken ct = default)
        {
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            if (arquivo == null || arquivo.Length == 0)
            {
                return BadRequest("Arquivo não fornecido.");
            }

            try
            {
                await _chamadosService.UploadAnexoAsync(id, arquivo, ct);
                return RedirectToPage("/Chamados/Detalhes", new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao fazer upload de anexo no chamado {ChamadoId}", id);
                return BadRequest($"Erro ao enviar anexo: {ex.Message}");
            }
        }

        public async Task<IActionResult> OnGetDownloadAnexoAsync(long id, long anexoId, CancellationToken ct = default)
        {
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            try
            {
                var stream = await _chamadosService.DownloadAnexoAsync(id, anexoId, ct);
                var anexos = await _chamadosService.ListarAnexosAsync(id, ct);
                var anexo = anexos?.FirstOrDefault(a => a.AnexoId == anexoId);
                
                if (anexo == null)
                {
                    return NotFound();
                }

                return File(stream, anexo.ContentType ?? "application/octet-stream", anexo.NomeArquivo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao fazer download do anexo {AnexoId} do chamado {ChamadoId}", anexoId, id);
                return NotFound();
            }
        }

        public string GetStatusBadgeClass(byte statusId)
        {
            return statusId switch
            {
                1 => "bg-info text-white",      // Aberto - azul claro
                2 => "bg-primary text-white",   // Em Andamento
                3 => "bg-warning text-dark",    // Pendente
                4 => "bg-success text-white",   // Resolvido
                5 => "bg-secondary text-white", // Fechado
                6 => "bg-danger text-white",    // Cancelado
                _ => "bg-secondary text-white"
            };
        }

        public string GetPrioridadeBadgeClass(byte prioridadeId)
        {
            return prioridadeId switch
            {
                1 => "bg-success text-white",   // Baixa
                2 => "bg-info text-white",      // Média
                3 => "bg-warning text-dark",    // Alta
                4 => "bg-warning text-dark",    // Urgente - amarelo conforme imagem
                _ => "bg-secondary text-white"
            };
        }

        public string FormatFileSize(long? bytes)
        {
            if (!bytes.HasValue || bytes.Value == 0)
                return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes.Value;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}

