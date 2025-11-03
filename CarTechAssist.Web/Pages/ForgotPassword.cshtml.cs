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
                    bool success = true;
                    string? codigo = null;
                    string? mensagemErro = null;

                    try
                    {
                        // Converter para JsonElement para ler propriedades
                        var jsonString = System.Text.Json.JsonSerializer.Serialize(resultado);
                        var jsonDoc = JsonDocument.Parse(jsonString);
                        var root = jsonDoc.RootElement;

                        // Verificar se há erro na resposta
                        if (root.TryGetProperty("success", out var successElement))
                            success = successElement.GetBoolean();
                        
                        if (root.TryGetProperty("message", out var messageElement))
                            mensagemErro = messageElement.GetString();

                        if (root.TryGetProperty("emailEnviado", out var emailEnviadoElement))
                            emailEnviado = emailEnviadoElement.GetBoolean();
                        if (root.TryGetProperty("usuarioEncontrado", out var usuarioEncontradoElement))
                            usuarioEncontrado = usuarioEncontradoElement.GetBoolean();
                        if (root.TryGetProperty("codigo", out var codigoElement) && codigoElement.ValueKind != JsonValueKind.Null)
                            codigo = codigoElement.GetString();
                        
                        // Verificar se há erro específico
                        if (root.TryGetProperty("error", out var errorElement))
                        {
                            var errorValue = errorElement.GetString();
                            if (errorValue == "EmailInvalido" || !string.IsNullOrEmpty(mensagemErro))
                            {
                                ErrorMessage = mensagemErro ?? "O email informado não corresponde ao cadastrado. Verifique o email e tente novamente.";
                                return Page();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Não foi possível ler propriedades da resposta de recuperação");
                        // Se não conseguir acessar, assume valores padrão
                    }

                    // Se success = false, significa que houve erro de validação
                    if (!success && !string.IsNullOrEmpty(mensagemErro))
                    {
                        ErrorMessage = mensagemErro;
                        return Page();
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
                    if (emailEnviado && success)
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

                // Tentar extrair mensagem da API do Data da exceção
                if (ex.Data.Contains("Message") && ex.Data["Message"] != null)
                {
                    var apiMessage = ex.Data["Message"]?.ToString();
                    if (!string.IsNullOrEmpty(apiMessage))
                    {
                        ErrorMessage = apiMessage;
                        return Page();
                    }
                }

                // Tentar extrair do ResponseContent
                if (ex.Data.Contains("ResponseContent") && ex.Data["ResponseContent"] != null)
                {
                    var responseContent = ex.Data["ResponseContent"]?.ToString();
                    if (!string.IsNullOrEmpty(responseContent))
                    {
                        try
                        {
                            var jsonDoc = JsonDocument.Parse(responseContent);
                            var root = jsonDoc.RootElement;
                            
                            if (root.TryGetProperty("message", out var messageElement))
                            {
                                var msg = messageElement.GetString();
                                if (!string.IsNullOrEmpty(msg))
                                {
                                    ErrorMessage = msg;
                                    return Page();
                                }
                            }
                        }
                        catch
                        {
                            // Ignora se não conseguir parsear
                        }
                    }
                }

                // Verificar se a mensagem da exceção contém informações sobre erro de validação
                if (ex.Message.Contains("EmailInvalido") || ex.Message.Contains("email informado não corresponde") || ex.Message.Contains("email não corresponde"))
                {
                    ErrorMessage = "O email informado não corresponde ao cadastrado. Verifique o email e tente novamente.";
                    return Page();
                }

                if (ex.Message.Contains("Usuário não encontrado") || ex.Message.Contains("usuario não encontrado"))
                {
                    ErrorMessage = "Usuário não encontrado. Verifique o login e tente novamente.";
                    return Page();
                }

                if (ex.Message.Contains("timeout") || ex.Message.Contains("Connection"))
                {
                    ErrorMessage = "Erro ao conectar com o servidor. Verifique se a API está em execução.";
                    return Page();
                }

                // Para outros erros HTTP, tentar extrair mensagem do response
                ErrorMessage = "Erro ao solicitar recuperação de senha. Verifique os dados e tente novamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao solicitar recuperação: {Message}", ex.Message);
                
                // Verificar se a exceção contém mensagem de validação
                if (ex.Message.Contains("EmailInvalido") || ex.Message.Contains("email informado não corresponde") || ex.Message.Contains("email não corresponde"))
                {
                    ErrorMessage = "O email informado não corresponde ao cadastrado. Verifique o email e tente novamente.";
                    return Page();
                }

                ErrorMessage = "Erro inesperado ao processar solicitação. Tente novamente mais tarde.";
            }

            return Page();
        }
    }
}

