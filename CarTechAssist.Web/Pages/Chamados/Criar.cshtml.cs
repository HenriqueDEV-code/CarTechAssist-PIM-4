using CarTechAssist.Contracts.Tickets;
using CarTechAssist.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Linq;

namespace CarTechAssist.Web.Pages.Chamados
{
    public class CriarModel : PageModel
    {
        private readonly ChamadosService _chamadosService;
        private readonly CategoriasService _categoriasService;
        private readonly UsuariosService _usuariosService;
        private readonly ILogger<CriarModel> _logger;

        [BindProperty]
        public CriarChamadoRequest ChamadoRequest { get; set; } = new(
            Titulo: string.Empty,
            Descricao: string.Empty, // CORREÇÃO: Tornado obrigatório
            CategoriaId: 0, // CORREÇÃO: Será validado antes de enviar
            PrioridadeId: 2, // Média
            CanalId: 1, // Web
            SolicitanteUsuarioId: 0, // Será preenchido automaticamente
            ResponsavelUsuarioId: null,
            SLA_EstimadoFim: null // Será calculado automaticamente
        );

        public IReadOnlyList<CategoriaDto>? Categorias { get; set; }
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }
        public int TenantId { get; set; }
        public int UsuarioId { get; set; }

        public byte TipoUsuarioId { get; set; }
        public IReadOnlyList<CarTechAssist.Contracts.Usuarios.UsuarioDto>? Usuarios { get; set; }
        public IReadOnlyList<CarTechAssist.Contracts.Usuarios.UsuarioDto>? Tecnicos { get; set; }

        public CriarModel(
            ChamadosService chamadosService,
            CategoriasService categoriasService,
            UsuariosService usuariosService,
            ILogger<CriarModel> logger)
        {
            _chamadosService = chamadosService;
            _categoriasService = categoriasService;
            _usuariosService = usuariosService;
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

            var tipoUsuarioIdStr = HttpContext.Session.GetString("TipoUsuarioId");
            if (byte.TryParse(tipoUsuarioIdStr, out var tipoUsuarioId))
            {
                TipoUsuarioId = tipoUsuarioId;
            }
            else
            {
                TipoUsuarioId = 1; // Padrão: Cliente
            }

            if (TipoUsuarioId == 1) // Cliente
            {
                ChamadoRequest = ChamadoRequest with { SolicitanteUsuarioId = usuarioId };
            }
            else
            {

                try
                {
                    var usuariosResult = await _usuariosService.ListarAsync(tipo: null, ativo: true, page: 1, pageSize: 1000, ct);
                    Usuarios = usuariosResult?.Items;

                    var tecnicosResult = await _usuariosService.ListarAsync(tipo: 2, ativo: true, page: 1, pageSize: 1000, ct);
                    Tecnicos = tecnicosResult?.Items;

                    if (TipoUsuarioId == 2 || TipoUsuarioId == 3) // Técnico ou Admin
                    {
                        ChamadoRequest = ChamadoRequest with { ResponsavelUsuarioId = usuarioId };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao carregar usuários");
                }
            }

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

            if (string.IsNullOrWhiteSpace(ChamadoRequest.Titulo))
            {
                ModelState.AddModelError(nameof(ChamadoRequest.Titulo), "Título é obrigatório.");
            }

            if (string.IsNullOrWhiteSpace(ChamadoRequest.Descricao))
            {
                ModelState.AddModelError(nameof(ChamadoRequest.Descricao), "Descrição é obrigatória.");
            }

            if (ChamadoRequest.CategoriaId <= 0)
            {
                ModelState.AddModelError(nameof(ChamadoRequest.CategoriaId), "Categoria é obrigatória. Selecione uma categoria.");
            }

            if (ChamadoRequest.PrioridadeId < 1 || ChamadoRequest.PrioridadeId > 4)
            {
                ModelState.AddModelError(nameof(ChamadoRequest.PrioridadeId), "Prioridade inválida.");
            }

            if (ChamadoRequest.CanalId < 1 || ChamadoRequest.CanalId > 4)
            {
                ModelState.AddModelError(nameof(ChamadoRequest.CanalId), "Canal inválido.");
            }

            if (!ModelState.IsValid)
            {
                try
                {
                    Categorias = await _categoriasService.ListarAsync(ct);
                }
                catch { }
                ErrorMessage = "Por favor, corrija os erros no formulário.";
                return Page();
            }

            var tipoUsuarioIdStr = HttpContext.Session.GetString("TipoUsuarioId");
            byte tipoUsuarioId = 1;
            if (byte.TryParse(tipoUsuarioIdStr, out var tipo))
            {
                tipoUsuarioId = tipo;
            }

            if (tipoUsuarioId == 1)
            {
                ChamadoRequest = ChamadoRequest with { SolicitanteUsuarioId = usuarioId };
            }

            else if (ChamadoRequest.SolicitanteUsuarioId <= 0)
            {
                ChamadoRequest = ChamadoRequest with { SolicitanteUsuarioId = usuarioId };
            }

            if ((tipoUsuarioId == 2 || tipoUsuarioId == 3) && !ChamadoRequest.ResponsavelUsuarioId.HasValue)
            {
                ChamadoRequest = ChamadoRequest with { ResponsavelUsuarioId = usuarioId };
            }

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

                if (ex.Message.Contains("Categoria") || ex.Message.Contains("categoria"))
                {
                    ErrorMessage = "Categoria inválida ou não encontrada. Por favor, selecione uma categoria válida.";
                    ModelState.AddModelError(nameof(ChamadoRequest.CategoriaId), "Categoria inválida.");
                }
                else if (ex.Message.Contains("Título") || ex.Message.Contains("título"))
                {
                    ErrorMessage = "Título inválido. Verifique o título e tente novamente.";
                    ModelState.AddModelError(nameof(ChamadoRequest.Titulo), ex.Message);
                }
                else if (ex.Message.Contains("Descrição") || ex.Message.Contains("descrição"))
                {
                    ErrorMessage = "Descrição inválida. Verifique a descrição e tente novamente.";
                    ModelState.AddModelError(nameof(ChamadoRequest.Descricao), ex.Message);
                }
                else
                {
                    ErrorMessage = $"Erro ao criar chamado: {ex.Message}. Verifique os dados e tente novamente.";
                }
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

