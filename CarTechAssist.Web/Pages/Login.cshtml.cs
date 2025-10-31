using CarTechAssist.Contracts.Auth;
using CarTechAssist.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarTechAssist.Web.Pages
{
    public class LoginModel : PageModel
    {
        private readonly AuthService _authService;
        private readonly ILogger<LoginModel> _logger;

        [BindProperty]
        public LoginRequest LoginRequest { get; set; } = new(string.Empty, string.Empty, 0);

        public string? ErrorMessage { get; set; }

        public LoginModel(AuthService authService, ILogger<LoginModel> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        public void OnGet()
        {
            // Se já estiver logado, redirecionar para dashboard
            var token = HttpContext.Session.GetString("Token");
            if (!string.IsNullOrEmpty(token))
            {
                Response.Redirect("/Dashboard");
            }
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                // Define TenantId automaticamente: querystring tem prioridade, senão usa 1 (padrão)
                var tenantFromQuery = HttpContext.Request.Query["tenant"].FirstOrDefault();
                if (int.TryParse(tenantFromQuery, out var tenantIdFromQuery))
                {
                    LoginRequest = LoginRequest with { TenantId = tenantIdFromQuery };
                }
                else if (LoginRequest.TenantId <= 0)
                {
                    LoginRequest = LoginRequest with { TenantId = 1 };
                }

                var response = await _authService.LoginAsync(LoginRequest, ct);
                
                if (response == null)
                {
                    ErrorMessage = "Login ou senha inválidos.";
                    return Page();
                }

                // Salvar informações na sessão
                HttpContext.Session.SetString("Token", response.Token);
                HttpContext.Session.SetString("RefreshToken", response.RefreshToken);
                HttpContext.Session.SetString("UsuarioId", response.UsuarioId.ToString());
                HttpContext.Session.SetString("TenantId", response.TenantId.ToString());
                HttpContext.Session.SetString("NomeCompleto", response.NomeCompleto);
                HttpContext.Session.SetString("TipoUsuarioId", response.TipoUsuarioId.ToString());

                return RedirectToPage("/Dashboard");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro ao fazer login: {Message}", ex.Message);
                
                // Mensagens mais específicas baseadas no tipo de erro
                if (ex.InnerException is TaskCanceledException || ex.Message.Contains("timeout"))
                {
                    ErrorMessage = "Tempo de conexão esgotado. Verifique se a API está em execução.";
                }
                else if (ex.Message.Contains("Connection refused") || ex.Message.Contains("No connection") || ex.Message.Contains("refused"))
                {
                    ErrorMessage = "Não foi possível conectar ao servidor. Verifique se a API está em execução em https://localhost:7294 ou http://localhost:5167";
                }
                else if (ex.Message.Contains("SSL") || ex.Message.Contains("certificate"))
                {
                    ErrorMessage = "Erro de certificado SSL. Isso pode ocorrer em desenvolvimento. Verifique se a API está rodando.";
                }
                else
                {
                    ErrorMessage = $"Erro ao conectar com o servidor: {ex.Message}. Verifique se a API está em execução em https://localhost:7294";
                }
                
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao fazer login");
                ErrorMessage = "Erro inesperado ao fazer login. Tente novamente.";
                return Page();
            }
        }

        public IActionResult OnGetLogout()
        {
            HttpContext.Session.Clear();
            return RedirectToPage("/Login");
        }
    }
}

