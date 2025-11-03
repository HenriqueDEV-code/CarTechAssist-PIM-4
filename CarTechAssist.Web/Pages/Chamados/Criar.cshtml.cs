using CarTechAssist.Contracts.Tickets;
using CarTechAssist.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarTechAssist.Web.Pages.Chamados
{
    public class CriarModel : PageModel
    {
        private readonly ChamadosService _chamadosService;
        private readonly CategoriasService _categoriasService;
        private readonly ILogger<CriarModel> _logger;

        [BindProperty]
        public CriarChamadoRequest ChamadoRequest { get; set; } = new(
            Titulo: string.Empty,
            Descricao: null,
            CategoriaId: null,
            PrioridadeId: 2, // Média
            CanalId: 1, // Web
            SolicitanteUsuarioId: 0,
            ResponsavelUsuarioId: null
        );

        public IReadOnlyList<CategoriaDto>? Categorias { get; set; }
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }
        public int TenantId { get; set; }
        public int UsuarioId { get; set; }

        public CriarModel(
            ChamadosService chamadosService,
            CategoriasService categoriasService,
            ILogger<CriarModel> logger)
        {
            _chamadosService = chamadosService;
            _categoriasService = categoriasService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
        {
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            var tenantIdStr = HttpContext.Session.GetString("TenantId");
            var usuarioIdStr = HttpContext.Session.GetString("UsuarioId");

            if (!int.TryParse(tenantIdStr, out var tenantId))
                tenantId = 1;
            TenantId = tenantId;

            if (!int.TryParse(usuarioIdStr, out var usuarioId))
            {
                return RedirectToPage("/Login");
            }
            UsuarioId = usuarioId;

            ChamadoRequest = ChamadoRequest with { SolicitanteUsuarioId = usuarioId };

            try
            {
                Categorias = await _categoriasService.ListarAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar categorias");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
        {
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            var usuarioIdStr = HttpContext.Session.GetString("UsuarioId");
            if (!int.TryParse(usuarioIdStr, out var usuarioId))
            {
                return RedirectToPage("/Login");
            }

            if (!ModelState.IsValid)
            {
                try
                {
                    Categorias = await _categoriasService.ListarAsync(ct);
                }
                catch { }
                return Page();
            }

            // Garantir que o solicitante é o usuário logado
            ChamadoRequest = ChamadoRequest with { SolicitanteUsuarioId = usuarioId };

            try
            {
                var resultado = await _chamadosService.CriarAsync(ChamadoRequest, ct);
                if (resultado != null)
                {
                    SuccessMessage = "Chamado criado com sucesso!";
                    return RedirectToPage("/Chamados/Detalhes", new { id = resultado.ChamadoId });
                }
                else
                {
                    ErrorMessage = "Erro ao criar chamado. Tente novamente.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar chamado");
                ErrorMessage = "Erro ao criar chamado. Verifique os dados e tente novamente.";
            }

            try
            {
                Categorias = await _categoriasService.ListarAsync(ct);
            }
            catch { }

            return Page();
        }
    }
}

