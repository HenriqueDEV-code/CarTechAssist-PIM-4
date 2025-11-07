using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CarTechAssist.Web.Services;

namespace CarTechAssist.Web.Pages
{
    public class ResetPasswordModel : PageModel
    {
        private readonly ApiClientService _apiClient;
        private readonly ILogger<ResetPasswordModel> _logger;

        [BindProperty]
        public string Codigo { get; set; } = string.Empty;

        [BindProperty]
        public string NovaSenha { get; set; } = string.Empty;

        [BindProperty]
        public string ConfirmarSenha { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }
        public bool SenhaRedefinida { get; set; }

        public ResetPasswordModel(ApiClientService apiClient, ILogger<ResetPasswordModel> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        public void OnGet()
        {

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

            if (NovaSenha != ConfirmarSenha)
            {
                ErrorMessage = "As senhas não coincidem.";
                return Page();
            }

            if (NovaSenha.Length < 6)
            {
                ErrorMessage = "A senha deve ter no mínimo 6 caracteres.";
                return Page();
            }

            try
            {

                var codigoLimpo = Codigo?.Replace(" ", "").Trim() ?? string.Empty;

                var request = new CarTechAssist.Contracts.Auth.RedefinirSenhaRequest(
                    codigoLimpo,
                    NovaSenha
                );

                var resultado = await _apiClient.PostAsync<object>("api/recuperacaosenha/redefinir", request, ct);

                if (resultado != null)
                {
                    SuccessMessage = "Senha redefinida com sucesso! Você já pode fazer login.";
                    SenhaRedefinida = true;
                    ModelState.Clear();

                    await Task.Delay(2000);
                    
                    return RedirectToPage("/Login");
                }

                ErrorMessage = "Erro ao redefinir senha. Verifique se o código está correto e não expirou.";
                return Page();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro ao redefinir senha: {Message}", ex.Message);

                if (ex.Message.Contains("Código inválido") || ex.Message.Contains("expirado"))
                {
                    ErrorMessage = "Código inválido ou expirado. Solicite um novo código.";
                }
                else if (ex.Message.Contains("timeout") || ex.Message.Contains("Connection"))
                {
                    ErrorMessage = "Erro ao conectar com o servidor. Verifique se a API está em execução.";
                }
                else
                {
                    ErrorMessage = $"Erro ao redefinir senha: {ex.Message}";
                }

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao redefinir senha");
                ErrorMessage = "Erro inesperado ao redefinir senha. Tente novamente.";
                return Page();
            }
        }
    }
}

