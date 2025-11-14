using CarTechAssist.Contracts.Common;
using CarTechAssist.Contracts.Enums;
using CarTechAssist.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarTechAssist.Web.Pages
{
    public class DashboardModel : PageModel
    {
        private readonly ChamadosService _chamadosService;
        private readonly ILogger<DashboardModel> _logger;

        public int TotalChamados { get; set; }
        public int TotalChamadosAbertos { get; set; }
        public int TotalChamadosEmAndamento { get; set; }
        public int TotalChamadosResolvidos { get; set; }
        public List<TicketView>? ChamadosRecentes { get; set; }
        
        public string NomeUsuario { get; set; } = string.Empty;
        public int TenantId { get; set; }
        public int UsuarioId { get; set; }
        public bool IsCliente { get; set; }

        public DashboardModel(ChamadosService chamadosService, ILogger<DashboardModel> logger)
        {
            _chamadosService = chamadosService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
        {
            // Verificar autenticação
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            // Obter informações da sessão
            var tenantIdStr = HttpContext.Session.GetString("TenantId");
            var usuarioIdStr = HttpContext.Session.GetString("UsuarioId");
            var tipoUsuarioIdStr = HttpContext.Session.GetString("TipoUsuarioId");
            NomeUsuario = HttpContext.Session.GetString("NomeCompleto") ?? "Usuário";
            
            // Verificar se é cliente
            IsCliente = !string.IsNullOrEmpty(tipoUsuarioIdStr) && tipoUsuarioIdStr == "1";

            int tempTenantId;
            if (!int.TryParse(tenantIdStr, out tempTenantId))
            {
                tempTenantId = 1;
            }
            TenantId = tempTenantId;

            int tempUsuarioId;
            if (!int.TryParse(usuarioIdStr, out tempUsuarioId))
            {
                return RedirectToPage("/Login");
            }
            UsuarioId = tempUsuarioId;

            try
            {
                // Carregar todos os chamados (sem paginação para estatísticas)
                var resultadoCompleto = await _chamadosService.ListarAsync(page: 1, pageSize: 1000, ct: ct);
                
                if (resultadoCompleto?.Items != null)
                {
                    var todosChamados = resultadoCompleto.Items.ToList();
                    
                    ChamadosRecentes = todosChamados.Take(10).ToList();
                    
                    TotalChamados = resultadoCompleto.Total;
                    TotalChamadosAbertos = todosChamados.Count(c => c.StatusNome == "1" || c.StatusNome?.ToLower().Contains("aberto") == true);
                    TotalChamadosEmAndamento = todosChamados.Count(c => c.StatusNome == "2" || c.StatusNome?.ToLower().Contains("andamento") == true);
                    TotalChamadosResolvidos = todosChamados.Count(c => c.StatusNome == "3" || c.StatusNome?.ToLower().Contains("resolvido") == true);
                }
                else
                {
                    ChamadosRecentes = new List<TicketView>();
                    TotalChamados = 0;
                    TotalChamadosAbertos = 0;
                    TotalChamadosEmAndamento = 0;
                    TotalChamadosResolvidos = 0;
                }

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar dashboard");
                // Mesmo com erro, mostra a página com valores zerados
                ChamadosRecentes = new List<TicketView>();
                return Page();
            }
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

        public string GetPrioridadeNome(string? prioridadeNome)
        {
            if (string.IsNullOrEmpty(prioridadeNome))
                return "Desconhecida";

            return prioridadeNome switch
            {
                "1" => "Baixa",
                "2" => "Normal",
                "3" => "Alta",
                "4" => "Crítica",
                _ => prioridadeNome
            };
        }
    }
}