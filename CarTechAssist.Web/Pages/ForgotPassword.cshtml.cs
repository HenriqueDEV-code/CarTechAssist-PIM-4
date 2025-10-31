using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CarTechAssist.Web.Services;
using System.Text.Json;

namespace CarTechAssist.Web.Pages
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly ApiClientService _apiClient;
        private readonly ILogger<ForgotPasswordModel> _logger;

        public ForgotPasswordModel(ApiClientService apiClient, ILogger<ForgotPasswordModel> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        [BindProperty]
        public string Login { get; set; } = string.Empty;

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }
        public string? InfoMessage { get; set; }
        public bool EmailSent { get; set; }
        public string? CodigoRecuperacao { get; set; }
        public bool EmailFalhou { get; set; }


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
            
            _logger.LogInformation("Solicitação de recuperação de senha - Login: {Login}, Email: {Email}", Login, Email);

            try
            {
                var request = new
                {
                    login = Login,
                    email = Email
                };

                var resultado = await _apiClient.PostAsync<object>("api/recuperacaosenha/solicitar", request, ct);

                if (resultado != null)
                {
                    // Tentar ler propriedades do resultado JSON
                    bool emailEnviado = true;
                    bool usuarioEncontrado = true;
                    string? codigo = null;

                    try
                    {
                        // Converter para JsonElement para ler propriedades
                        var jsonString = System.Text.Json.JsonSerializer.Serialize(resultado);
                        var jsonDoc = JsonDocument.Parse(jsonString);
                        var root = jsonDoc.RootElement;

                        if (root.TryGetProperty("emailEnviado", out var emailEnviadoElement))
                            emailEnviado = emailEnviadoElement.GetBoolean();
                        if (root.TryGetProperty("usuarioEncontrado", out var usuarioEncontradoElement))
                            usuarioEncontrado = usuarioEncontradoElement.GetBoolean();
                        if (root.TryGetProperty("codigo", out var codigoElement) && codigoElement.ValueKind != JsonValueKind.Null)
                            codigo = codigoElement.GetString();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Não foi possível ler propriedades da resposta de recuperação");
                        // Se não conseguir acessar, assume valores padrão
                    }

                    if (!usuarioEncontrado)
                    {
                        ErrorMessage = "Usuário não encontrado ou email não corresponde.";
                    }
                    else if (!emailEnviado)
                    {
                        // Email não foi enviado - mostrar código na tela (modo debug)
                        EmailFalhou = true;
                        CodigoRecuperacao = codigo;
                        SuccessMessage = $"⚠️ O email não pôde ser enviado, mas o código foi gerado!";
                        InfoMessage = $"Código de recuperação: {codigo} (Use este código para redefinir sua senha)";
                        EmailSent = true;
                    }
                    else
                    {
                        SuccessMessage = "Se o usuário e e-mail estiverem corretos, você receberá um código de recuperação por e-mail em breve.";
                        InfoMessage = "Verifique sua caixa de entrada e spam. O código é válido por 30 minutos.";
                        EmailSent = true;
                    }
                    
                    // Limpar campos apenas se email foi enviado
                    if (emailEnviado)
                    {
                        Login = string.Empty;
                        Email = string.Empty;
                        ModelState.Clear();
                    }
                }
                else
                {
                    ErrorMessage = "Erro ao solicitar recuperação de senha. Tente novamente.";
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro ao solicitar recuperação: {Message}", ex.Message);

                if (ex.Message.Contains("timeout") || ex.Message.Contains("Connection"))
                {
                    ErrorMessage = "Erro ao conectar com o servidor. Verifique se a API está em execução.";
                }
                else
                {
                    // Por segurança, sempre mostrar mensagem de sucesso
                    SuccessMessage = "Se o usuário e e-mail estiverem corretos, você receberá um código de recuperação por e-mail em breve.";
                    InfoMessage = "Verifique sua caixa de entrada e spam. O código é válido por 30 minutos.";
                    EmailSent = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao solicitar recuperação");
                // Por segurança, mostrar mensagem de sucesso mesmo em erro
                SuccessMessage = "Se o usuário e e-mail estiverem corretos, você receberá um código de recuperação por e-mail em breve.";
                InfoMessage = "Verifique sua caixa de entrada e spam. O código é válido por 30 minutos.";
                EmailSent = true;
            }

            return Page();
        }
    }
}

