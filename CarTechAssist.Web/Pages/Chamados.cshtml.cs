using CarTechAssist.Contracts.Common;
using CarTechAssist.Contracts.Enums;
using CarTechAssist.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarTechAssist.Web.Pages
{
    public class ChamadosModel : PageModel
    {
        private readonly ChamadosService _chamadosService;
        private readonly ILogger<ChamadosModel> _logger;

        public PagedResult<TicketView>? Chamados { get; set; }
        public string? ErrorMessage { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public byte? StatusFiltro { get; set; }
        public int TenantId { get; set; }
        public int UsuarioId { get; set; }
        public byte TipoUsuarioId { get; set; }

        public ChamadosModel(ChamadosService chamadosService, ILogger<ChamadosModel> logger)
        {
            _chamadosService = chamadosService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync(
            byte? statusId = null,
            int page = 1,
            CancellationToken ct = default)
        {
            // Verificar autenticação
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

            CurrentPage = page;
            StatusFiltro = statusId;

            try
            {
                // Se for Cliente, filtrar apenas seus chamados
                int? solicitanteUsuarioId = tipoUsuarioId == 1 ? UsuarioId : null;

                Chamados = await _chamadosService.ListarAsync(
                    statusId: statusId,
                    solicitanteUsuarioId: solicitanteUsuarioId,
                    page: page,
                    pageSize: PageSize,
                    ct: ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar chamados");
                ErrorMessage = "Erro ao carregar chamados. Tente novamente.";
            }

            return Page();
        }

        public string GetStatusBadgeClass(string? statusNome)
        {
            if (string.IsNullOrEmpty(statusNome))
                return "bg-secondary";

            var statusLower = statusNome.ToLower();
            if (statusLower.Contains("aberto") || statusLower == "1")
                return "bg-warning";
            if (statusLower.Contains("andamento") || statusLower == "2")
                return "bg-info";
            if (statusLower.Contains("resolvido") || statusLower == "3")
                return "bg-success";
            if (statusLower.Contains("cancelado") || statusLower == "4")
                return "bg-secondary";

            return "bg-secondary";
        }

        public string GetStatusNome(string? statusNome)
        {
            if (string.IsNullOrEmpty(statusNome))
                return "Desconhecido";

            return statusNome switch
            {
                "1" => "Aberto",
                "2" => "Em Andamento",
                "3" => "Resolvido",
                "4" => "Cancelado",
                _ => statusNome
            };
        }

        public string GetPrioridadeBadgeClass(string? prioridadeNome)
        {
            if (string.IsNullOrEmpty(prioridadeNome))
                return "bg-secondary";

            var prioridadeLower = prioridadeNome.ToLower();
            if (prioridadeLower.Contains("baixa") || prioridadeLower == "1")
                return "bg-success";
            if (prioridadeLower.Contains("normal") || prioridadeLower == "2")
                return "bg-info";
            if (prioridadeLower.Contains("alta") || prioridadeLower == "3")
                return "bg-warning";
            if (prioridadeLower.Contains("crítica") || prioridadeLower == "4")
                return "bg-danger";

            return "bg-secondary";
        }

        public int GetTotalPages()
        {
            if (Chamados == null || Chamados.PageSize <= 0)
                return 0;
            return (int)Math.Ceiling((double)Chamados.Total / Chamados.PageSize);
        }
    }
}

