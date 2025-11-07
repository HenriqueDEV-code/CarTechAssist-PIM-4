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
                // Validar campos obrigatórios antes de enviar
                if (string.IsNullOrWhiteSpace(NovoUsuario.Login))
                {
                    ErrorMessage = "Login é obrigatório.";
                    MostrarFormulario = true;
                    return await OnGetAsync(ct: ct);
                }
                
                if (string.IsNullOrWhiteSpace(NovoUsuario.NomeCompleto))
                {
                    ErrorMessage = "Nome completo é obrigatório.";
                    MostrarFormulario = true;
                    return await OnGetAsync(ct: ct);
                }
                
                if (string.IsNullOrWhiteSpace(NovoUsuario.Senha))
                {
                    ErrorMessage = "Senha é obrigatória.";
                    MostrarFormulario = true;
                    return await OnGetAsync(ct: ct);
                }
                
                if (NovoUsuario.Senha.Length < 6)
                {
                    ErrorMessage = "A senha deve ter no mínimo 6 caracteres.";
                    MostrarFormulario = true;
                    return await OnGetAsync(ct: ct);
                }
                
                _logger.LogInformation("Enviando requisição para criar usuário. Login: {Login}, TipoUsuarioId: {TipoUsuarioId}", 
                    NovoUsuario.Login, NovoUsuario.TipoUsuarioId);
                
                var resultado = await _usuariosService.CriarAsync(NovoUsuario, ct);
                
                if (resultado != null)
                {
                    _logger.LogInformation("Usuário criado com sucesso. UsuarioId: {UsuarioId}, Login: {Login}", 
                        resultado.UsuarioId, resultado.Login);
                    
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
                else
                {
                    _logger.LogWarning("API retornou null ao criar usuário");
                    ErrorMessage = "Erro ao criar usuário. A API não retornou dados.";
                    MostrarFormulario = true;
                }
            }
            catch (System.Net.Http.HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Erro HTTP ao criar usuário. StatusCode: {StatusCode}, Message: {Message}",
                    httpEx.Data.Contains("StatusCode") ? httpEx.Data["StatusCode"] : "Desconhecido",
                    httpEx.Data.Contains("Message") ? httpEx.Data["Message"] : httpEx.Message);
                
                if (httpEx.Data.Contains("StatusCode") && httpEx.Data["StatusCode"] is System.Net.HttpStatusCode statusCode)
                {
                    if (statusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        var apiMessage = httpEx.Data.Contains("Message") ? httpEx.Data["Message"]?.ToString() : null;
                        ErrorMessage = !string.IsNullOrEmpty(apiMessage) ? apiMessage : "Dados inválidos. Verifique os campos e tente novamente.";
                    }
                    else if (statusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        ErrorMessage = "Sua sessão expirou. Por favor, faça login novamente.";
                        return RedirectToPage("/Login");
                    }
                    else
                    {
                        var apiMessage = httpEx.Data.Contains("Message") ? httpEx.Data["Message"]?.ToString() : null;
                        ErrorMessage = !string.IsNullOrEmpty(apiMessage) ? apiMessage : $"Erro ao criar usuário: {httpEx.Message}";
                    }
                }
                else
                {
                    ErrorMessage = $"Erro ao criar usuário: {httpEx.Message}";
                }
                MostrarFormulario = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao criar usuário. Tipo: {Type}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                ErrorMessage = $"Erro ao criar usuário: {ex.Message}";
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

