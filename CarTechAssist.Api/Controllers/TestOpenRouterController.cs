using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CarTechAssist.Application.Services;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace CarTechAssist.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TestOpenRouterController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<TestOpenRouterController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public TestOpenRouterController(
            IConfiguration configuration,
            ILogger<TestOpenRouterController> logger,
            IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost("test-api-key")]
        public async Task<IActionResult> TestApiKey([FromBody] TestApiKeyRequest? request = null)
        {
            try
            {
                // Usar a API Key do request ou do appsettings.json
                var apiKey = request?.ApiKey ?? _configuration["OpenRouter:ApiKey"]?.Trim();
                
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return BadRequest(new { 
                        success = false, 
                        message = "API Key não fornecida no request e não encontrada no appsettings.json" 
                    });
                }

                _logger.LogInformation("🧪 Testando API Key do OpenRouter:");
                _logger.LogInformation("   Tamanho: {Length} caracteres", apiKey.Length);
                _logger.LogInformation("   Prefixo: {Prefix}", apiKey.Substring(0, Math.Min(20, apiKey.Length)));
                _logger.LogInformation("   Sufixo: ...{Suffix}", apiKey.Length > 20 ? apiKey.Substring(apiKey.Length - 4) : "");
                _logger.LogInformation("   Começa com 'sk-or-v1-': {StartsWith}", apiKey.StartsWith("sk-or-v1-"));

                // Criar HttpClient
                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
                client.Timeout = TimeSpan.FromSeconds(30);

                // Criar requisição de teste simples
                var testRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new
                        {
                            model = "openai/gpt-4o-mini",
                            messages = new[]
                            {
                                new { role = "user", content = "Hello" }
                            },
                            max_tokens = 10
                        }),
                        Encoding.UTF8,
                        "application/json")
                };

                // Adicionar headers
                testRequest.Headers.Add("Authorization", $"Bearer {apiKey.Trim()}");
                testRequest.Headers.Add("HTTP-Referer", _configuration["OpenRouter:HttpReferer"] ?? "https://cartechassist.local");
                testRequest.Headers.Add("X-Title", "CarTechAssist");

                _logger.LogInformation("📤 Enviando requisição de teste para OpenRouter...");

                var response = await client.SendAsync(testRequest);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("📥 Resposta recebida:");
                _logger.LogInformation("   Status: {StatusCode} ({StatusName})", (int)response.StatusCode, response.StatusCode);
                _logger.LogInformation("   Content: {Content}", responseContent);

                if (response.IsSuccessStatusCode)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "API Key está funcionando corretamente!",
                        statusCode = (int)response.StatusCode,
                        response = JsonSerializer.Deserialize<object>(responseContent)
                    });
                }
                else
                {
                    return StatusCode((int)response.StatusCode, new
                    {
                        success = false,
                        message = $"API Key retornou erro {response.StatusCode}",
                        statusCode = (int)response.StatusCode,
                        response = responseContent
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao testar API Key");
                return StatusCode(500, new
                {
                    success = false,
                    message = $"Erro ao testar API Key: {ex.Message}",
                    errorType = ex.GetType().Name,
                    innerError = ex.InnerException?.Message
                });
            }
        }

        [HttpGet("config-info")]
        public IActionResult GetConfigInfo()
        {
            var apiKey = _configuration["OpenRouter:ApiKey"]?.Trim();
            
            return Ok(new
            {
                enabled = _configuration["OpenRouter:Enabled"],
                model = _configuration["OpenRouter:Model"],
                maxTokens = _configuration["OpenRouter:MaxTokens"],
                temperature = _configuration["OpenRouter:Temperature"],
                httpReferer = _configuration["OpenRouter:HttpReferer"],
                apiKeyPresent = !string.IsNullOrEmpty(apiKey),
                apiKeyLength = apiKey?.Length ?? 0,
                apiKeyPrefix = apiKey != null && apiKey.Length > 0 
                    ? apiKey.Substring(0, Math.Min(20, apiKey.Length)) 
                    : "N/A",
                apiKeySuffix = apiKey != null && apiKey.Length > 20 
                    ? "..." + apiKey.Substring(apiKey.Length - 4) 
                    : "N/A",
                apiKeyStartsWithCorrectPrefix = apiKey?.StartsWith("sk-or-v1-") ?? false
            });
        }
    }

    public record TestApiKeyRequest(string? ApiKey);
}

