using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;
using CarTechAssist.Domain.Interfaces;
using System.Net.Http.Json;
using System.Net.Http;

namespace CarTechAssist.Application.Services
{




    public class OpenRouterService : IAiProvider
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<OpenRouterService> _logger;
        private readonly HttpClient? _httpClient;
        private readonly bool _enabled;
        private readonly string _model;
        private readonly int _maxTokens;
        private readonly double _temperature;
        private readonly string? _apiKey;

        public OpenRouterService(IConfiguration configuration, ILogger<OpenRouterService> logger, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _enabled = bool.Parse(_configuration["OpenRouter:Enabled"] ?? "false");
            _model = _configuration["OpenRouter:Model"] ?? "openai/gpt-4o-mini";
            _maxTokens = int.Parse(_configuration["OpenRouter:MaxTokens"] ?? "1000");
            _temperature = double.Parse(_configuration["OpenRouter:Temperature"] ?? "0.7");
            _apiKey = _configuration["OpenRouter:ApiKey"];

            if (_enabled)
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    _logger.LogWarning("OpenRouter habilitado mas ApiKey não configurada");
                    _enabled = false;
                }
                else
                {
                    try
                    {
                        _httpClient = httpClientFactory.CreateClient();
                        _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
                        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

                        _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", _configuration["OpenRouter:HttpReferer"] ?? "https://cartechassist.local");
                        _httpClient.DefaultRequestHeaders.Add("X-Title", "CarTechAssist");
                        _httpClient.Timeout = TimeSpan.FromSeconds(30);
                        _logger.LogInformation("✅ OpenRouterService configurado com sucesso!");
                        _logger.LogInformation("   Model: {Model}, MaxTokens: {MaxTokens}, Temperature: {Temperature}", 
                            _model, _maxTokens, _temperature);
                        _logger.LogInformation("   API Base: https://openrouter.ai/api/v1/");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao configurar OpenRouterService");
                        _enabled = false;
                    }
                }
            }
        }

        public bool EstaHabilitado() => _enabled && _httpClient != null;

        public async Task<(string Provedor, string Modelo, string Mensagem, decimal? Confianca, string? ResumoRaciocinio, int? InputTokens, int? outputTokens, decimal? CustoUsd)> ResponderAsync(string prompt, CancellationToken ct)
        {
            if (!_enabled || _httpClient == null)
            {
                throw new InvalidOperationException("OpenRouter não está habilitado ou configurado corretamente.");
            }

            try
            {
                _logger.LogInformation("Enviando prompt para OpenRouter. Model: {Model}, PromptLength: {Length}", _model, prompt.Length);

                var systemPrompt = @"Você é um assistente técnico especializado do CarTechAssist, focado em ajudar clientes com problemas técnicos.

SUA PRINCIPAL FUNÇÃO:
- Ajudar clientes a diagnosticar e resolver problemas técnicos (sistemas, redes, logs, autenticação)
- Quando o problema não puder ser resolvido imediatamente, SUGERIR CRIAR UM CHAMADO
- Ser proativo: se o cliente mencionar problemas, erros, bugs ou necessidade de suporte, ofereça criar um chamado

DIRETRIZES:
1. Seja objetivo, claro e amigável
2. Tente resolver problemas simples com instruções diretas
3. Se o problema for complexo, urgente ou não tiver solução imediata, SEMPRE sugira criar um chamado
4. Use linguagem técnica mas acessível
5. Quando sugerir criar chamado, explique os benefícios (acompanhamento, prioridade, resolução garantida)

PALAVRAS-CHAVE que indicam necessidade de chamado:
- 'problema', 'erro', 'bug', 'não funciona', 'quebrado', 'travado', 'lento'
- 'preciso de ajuda', 'preciso de suporte', 'não consigo', 'urgente'
- Qualquer descrição de falha técnica que não tenha solução simples

SEMPRE termine sua resposta oferecendo criar um chamado se detectar necessidade de suporte técnico.";

                var requestBody = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = prompt }
                    },
                    temperature = _temperature,
                    max_tokens = _maxTokens
                };

                var response = await _httpClient.PostAsJsonAsync("chat/completions", requestBody, ct);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<OpenRouterResponse>(ct);
                
                if (result?.choices == null || result.choices.Length == 0)
                {
                    throw new Exception("Resposta vazia do OpenRouter");
                }

                var message = result.choices[0].message?.content ?? "Sem resposta";
                var usage = result.usage;

                _logger.LogInformation("OpenRouter respondeu. Model: {Model}, Tokens: {Tokens}", _model, usage?.total_tokens ?? 0);

                decimal? custoUsd = null;
                if (result.usage?.prompt_tokens.HasValue == true && result.usage?.completion_tokens.HasValue == true)
                {


                }

                return (
                    Provedor: "OpenRouter",
                    Modelo: _model,
                    Mensagem: message,
                    Confianca: 0.95m,
                    ResumoRaciocinio: $"Resposta gerada por {_model} via OpenRouter",
                    InputTokens: usage?.prompt_tokens,
                    outputTokens: usage?.completion_tokens,
                    CustoUsd: custoUsd
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao chamar OpenRouter");
                throw new Exception($"Erro ao comunicar com OpenRouter: {ex.Message}", ex);
            }
        }

        public async Task<string> ProcessarMensagemAsync(string mensagem, string sessionId, CancellationToken ct)
        {
            if (!EstaHabilitado())
            {
                throw new InvalidOperationException("OpenRouter não está habilitado");
            }

            var resultado = await ResponderAsync(mensagem, ct);
            return resultado.Mensagem;
        }

        private class OpenRouterResponse
        {
            public Choice[]? choices { get; set; }
            public Usage? usage { get; set; }
        }

        private class Choice
        {
            public Message? message { get; set; }
        }

        private class Message
        {
            public string? content { get; set; }
        }

        private class Usage
        {
            public int? prompt_tokens { get; set; }
            public int? completion_tokens { get; set; }
            public int? total_tokens { get; set; }
        }
    }
}

