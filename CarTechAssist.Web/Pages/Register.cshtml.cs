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

                _logger.LogInformation("Tentando criar conta de cliente. Login: {Login}, Email: {Email}", 
                    request.Login, request.Email);

                // CORREÇÃO: Usar endpoint público para registro de clientes
                var resultado = await _usuariosService.CriarPublicoAsync(request, ct);

                if (resultado == null)
                {
                    _logger.LogWarning("API retornou null ao criar conta de cliente");
                    ErrorMessage = "Erro ao criar conta. Tente novamente.";
                    return Page();
                }

                _logger.LogInformation("Conta de cliente criada com sucesso. UsuarioId: {UsuarioId}, Login: {Login}", 
                    resultado.UsuarioId, resultado.Login);

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
            catch (System.Net.Http.HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Erro HTTP ao criar conta. StatusCode: {StatusCode}, Message: {Message}",
                    httpEx.Data.Contains("StatusCode") ? httpEx.Data["StatusCode"] : "Desconhecido",
                    httpEx.Data.Contains("Message") ? httpEx.Data["Message"] : httpEx.Message);

                if (httpEx.Data.Contains("StatusCode") && httpEx.Data["StatusCode"] is System.Net.HttpStatusCode statusCode)
                {
                    if (statusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        var apiMessage = httpEx.Data.Contains("Message") ? httpEx.Data["Message"]?.ToString() : null;
                        ErrorMessage = !string.IsNullOrEmpty(apiMessage) ? apiMessage : "Dados inválidos. Verifique os campos e tente novamente.";
                    }
                    else if (statusCode == System.Net.HttpStatusCode.Conflict || statusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        var apiMessage = httpEx.Data.Contains("Message") ? httpEx.Data["Message"]?.ToString() : null;
                        if (!string.IsNullOrEmpty(apiMessage) && (apiMessage.Contains("já existe") || apiMessage.Contains("já está em uso")))
                        {
                            ErrorMessage = "Este usuário já existe. Tente outro nome de usuário ou faça login.";
                        }
                        else
                        {
                            ErrorMessage = !string.IsNullOrEmpty(apiMessage) ? apiMessage : "Erro ao criar conta. Verifique os dados e tente novamente.";
                        }
                    }
                    else
                    {
                        var apiMessage = httpEx.Data.Contains("Message") ? httpEx.Data["Message"]?.ToString() : null;
                        ErrorMessage = !string.IsNullOrEmpty(apiMessage) ? apiMessage : $"Erro ao criar conta: {httpEx.Message}";
                    }
                }
                else
                {
                    if (httpEx.Message.Contains("já existe") || httpEx.Message.Contains("already exists") || httpEx.Message.Contains("409"))
                    {
                        ErrorMessage = "Este usuário já existe. Tente outro nome de usuário ou faça login.";
                    }
                    else if (httpEx.Message.Contains("timeout") || httpEx.Message.Contains("Connection"))
                    {
                        ErrorMessage = "Erro ao conectar com o servidor. Verifique se a API está em execução.";
                    }
                    else
                    {
                        ErrorMessage = $"Erro ao criar conta: {httpEx.Message}";
                    }
                }

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao criar conta. Tipo: {Type}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                ErrorMessage = $"Erro inesperado ao criar conta: {ex.Message}";
                return Page();
            }
        }
    }
}

