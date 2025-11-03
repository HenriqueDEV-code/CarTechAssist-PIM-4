using CarTechAssist.Contracts.Common;
using CarTechAssist.Contracts.Usuarios;
using CarTechAssist.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarTechAssist.Web.Pages
{
    public class UsuariosModel : PageModel
    {
        private readonly UsuariosService _usuariosService;
        private readonly ILogger<UsuariosModel> _logger;

        public PagedResult<UsuarioDto>? Usuarios { get; set; }
        public string? ErrorMessage { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public byte? TipoFiltro { get; set; }

        [BindProperty]
        public CriarUsuarioRequest NovoUsuario { get; set; } = new(
            Login: string.Empty,
            NomeCompleto: string.Empty,
            Email: null,
            Telefone: null,
            TipoUsuarioId: 2, // Agente por padrão
            Senha: string.Empty
        );

        // Editar usuário
        [BindProperty]
        public int EditUsuarioId { get; set; }
        [BindProperty]
        public AtualizarUsuarioRequest EditUsuario { get; set; } = new(
            NomeCompleto: string.Empty,
            Email: null,
            Telefone: null,
            TipoUsuarioId: 2
        );

        public string? SuccessMessage { get; set; }
        public bool MostrarFormulario { get; set; }

        public UsuariosModel(UsuariosService usuariosService, ILogger<UsuariosModel> logger)
        {
            _usuariosService = usuariosService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync(
            byte? tipo = null,
            int page = 1,
            CancellationToken ct = default)
        {
            // Verificar autenticação
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            // Verificar se é Admin
            var tipoUsuarioIdStr = HttpContext.Session.GetString("TipoUsuarioId");
            if (!byte.TryParse(tipoUsuarioIdStr, out var tipoUsuarioId) || tipoUsuarioId != 3)
            {
                return RedirectToPage("/Dashboard");
            }

            CurrentPage = page;
            TipoFiltro = tipo;

            try
            {
                Usuarios = await _usuariosService.ListarAsync(
                    tipo: tipo,
                    page: page,
                    pageSize: PageSize,
                    ct: ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar usuários");
                ErrorMessage = "Erro ao carregar usuários. Tente novamente.";
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

            // Verificar se é Admin
            var tipoUsuarioIdStr = HttpContext.Session.GetString("TipoUsuarioId");
            if (!byte.TryParse(tipoUsuarioIdStr, out var tipoUsuarioId) || tipoUsuarioId != 3)
            {
                return RedirectToPage("/Dashboard");
            }

            if (!ModelState.IsValid)
            {
                await OnGetAsync(ct: ct);
                MostrarFormulario = true;
                return Page();
            }

            try
            {
                var resultado = await _usuariosService.CriarAsync(NovoUsuario, ct);
                if (resultado != null)
                {
                    SuccessMessage = $"Usuário '{resultado.NomeCompleto}' criado com sucesso!";
                    NovoUsuario = new CriarUsuarioRequest(
                        Login: string.Empty,
                        NomeCompleto: string.Empty,
                        Email: null,
                        Telefone: null,
                        TipoUsuarioId: 2,
                        Senha: string.Empty
                    );
                    ModelState.Clear();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar usuário");
                ErrorMessage = "Erro ao criar usuário. Verifique os dados e tente novamente.";
                MostrarFormulario = true;
            }

            return await OnGetAsync(ct: ct);
        }

        public async Task<IActionResult> OnPostEditarAsync(CancellationToken ct = default)
        {
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            var tipoUsuarioIdStr = HttpContext.Session.GetString("TipoUsuarioId");
            if (!byte.TryParse(tipoUsuarioIdStr, out var tipoUsuarioId) || tipoUsuarioId != 3)
            {
                return RedirectToPage("/Dashboard");
            }

            try
            {
                var atualizado = await _usuariosService.AtualizarAsync(EditUsuarioId, EditUsuario, ct);
                if (atualizado != null)
                {
                    SuccessMessage = $"Usuário '{atualizado.NomeCompleto}' atualizado com sucesso!";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar usuário {UsuarioId}", EditUsuarioId);
                ErrorMessage = "Erro ao atualizar usuário. Verifique os dados e tente novamente.";
            }

            return await OnGetAsync(ct: ct);
        }

        public async Task<IActionResult> OnPostToggleAtivoAsync(int usuarioId, bool ativoAtual, CancellationToken ct = default)
        {
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            var tipoUsuarioIdStr = HttpContext.Session.GetString("TipoUsuarioId");
            if (!byte.TryParse(tipoUsuarioIdStr, out var tipoUsuarioId) || tipoUsuarioId != 3)
            {
                return RedirectToPage("/Dashboard");
            }

            try
            {
                var request = new AlterarAtivacaoRequest(!ativoAtual);
                var atualizado = await _usuariosService.AtivarDesativarAsync(usuarioId, request, ct);
                if (atualizado != null)
                {
                    SuccessMessage = $"Usuário '{atualizado.NomeCompleto}' {(atualizado.Ativo ? "ativado" : "desativado")} com sucesso!";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao alterar ativação do usuário {UsuarioId}", usuarioId);
                ErrorMessage = "Erro ao alterar ativação. Tente novamente.";
            }

            return await OnGetAsync(ct: ct);
        }

        public string GetTipoUsuarioNome(byte tipoUsuarioId)
        {
            return tipoUsuarioId switch
            {
                1 => "Cliente",
                2 => "Agente",
                3 => "Admin",
                4 => "Bot",
                _ => "Desconhecido"
            };
        }

        public string GetTipoUsuarioBadgeClass(byte tipoUsuarioId)
        {
            return tipoUsuarioId switch
            {
                1 => "bg-info",
                2 => "bg-primary",
                3 => "bg-danger",
                4 => "bg-secondary",
                _ => "bg-secondary"
            };
        }

        public int GetTotalPages()
        {
            if (Usuarios == null || Usuarios.PageSize <= 0)
                return 0;
            return (int)Math.Ceiling((double)Usuarios.Total / Usuarios.PageSize);
        }
    }
}

