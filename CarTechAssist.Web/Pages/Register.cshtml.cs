using CarTechAssist.Contracts.Usuarios;
using CarTechAssist.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarTechAssist.Web.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly UsuariosService _usuariosService;
        private readonly ILogger<RegisterModel> _logger;

        [BindProperty]
        public CriarUsuarioRequest RegisterRequest { get; set; } = new(
            string.Empty,
            string.Empty,
            null,
            null,
            1, // TipoUsuarioId = Cliente
            string.Empty
        );

        [BindProperty]
        public string ConfirmarSenha { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public RegisterModel(UsuariosService usuariosService, ILogger<RegisterModel> logger)
        {
            _usuariosService = usuariosService;
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

            // Validar confirmação de senha
            if (RegisterRequest.Senha != ConfirmarSenha)
            {
                ErrorMessage = "As senhas não coincidem.";
                return Page();
            }

            // Validar senha mínima
            if (RegisterRequest.Senha.Length < 6)
            {
                ErrorMessage = "A senha deve ter no mínimo 6 caracteres.";
                return Page();
            }

            try
            {
                // Garantir que é cliente
                var request = RegisterRequest with { TipoUsuarioId = 1 }; // Cliente

                // CORREÇÃO: Usar endpoint público para registro de clientes
                var resultado = await _usuariosService.CriarPublicoAsync(request, ct);

                if (resultado == null)
                {
                    ErrorMessage = "Erro ao criar conta. Tente novamente.";
                    return Page();
                }

                SuccessMessage = "Conta criada com sucesso! Você já pode fazer login.";
                ModelState.Clear();
                RegisterRequest = new CriarUsuarioRequest(
                    string.Empty,
                    string.Empty,
                    null,
                    null,
                    1,
                    string.Empty
                );
                ConfirmarSenha = string.Empty;

                // Aguardar um pouco para mostrar mensagem de sucesso
                await Task.Delay(2000);

                return RedirectToPage("/Login");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro ao criar conta: {Message}", ex.Message);

                if (ex.Message.Contains("já existe") || ex.Message.Contains("already exists") || ex.Message.Contains("409"))
                {
                    ErrorMessage = "Este usuário já existe. Tente outro nome de usuário ou faça login.";
                }
                else if (ex.Message.Contains("timeout") || ex.Message.Contains("Connection"))
                {
                    ErrorMessage = "Erro ao conectar com o servidor. Verifique se a API está em execução.";
                }
                else
                {
                    ErrorMessage = $"Erro ao criar conta: {ex.Message}";
                }

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao criar conta");
                ErrorMessage = "Erro inesperado ao criar conta. Tente novamente.";
                return Page();
            }
        }
    }
}

