using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;
using CarTechAssist.Domain.Interfaces;
using System.Net.Http.Json;
using System.Net.Http;

namespace CarTechAssist.Application.Services
{
    public class OpenAIService : IAiProvider
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<OpenAIService> _logger;
        private readonly HttpClient? _httpClient;
        private readonly bool _enabled;
        private readonly string _model;
        private readonly int _maxTokens;
        private readonly double _temperature;
        private readonly string? _apiKey;

        public OpenAIService(IConfiguration configuration, ILogger<OpenAIService> logger, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _enabled = bool.Parse(_configuration["OpenAI:Enabled"] ?? "false");
            _model = _configuration["OpenAI:Model"] ?? "gpt-4o-mini";
            _maxTokens = int.Parse(_configuration["OpenAI:MaxTokens"] ?? "1000");
            _temperature = double.Parse(_configuration["OpenAI:Temperature"] ?? "0.7");
            _apiKey = _configuration["OpenAI:ApiKey"];

            if (_enabled)
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    _logger.LogWarning("OpenAI habilitado mas ApiKey não configurada");
                    _enabled = false;
                }
                else
                {
                    try
                    {
                        _httpClient = httpClientFactory.CreateClient();
                        _httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
                        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
                        _httpClient.Timeout = TimeSpan.FromSeconds(30);
                        _logger.LogInformation("✅ OpenAIService configurado com sucesso!");
                        _logger.LogInformation("   Model: {Model}, MaxTokens: {MaxTokens}, Temperature: {Temperature}", 
                            _model, _maxTokens, _temperature);
                        _logger.LogInformation("   API Base: https://api.openai.com/v1/");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao configurar OpenAIService");
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
                throw new InvalidOperationException("OpenAI não está habilitado ou configurado corretamente.");
            }

            try
            {
                _logger.LogInformation("Enviando prompt para OpenAI. Model: {Model}, PromptLength: {Length}", _model, prompt.Length);

                var requestBody = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "system", content = "Você é um assistente técnico especializado em suporte para sistemas, redes e logs. Sua função é ajudar usuários a diagnosticar problemas técnicos e sugerir soluções. Seja objetivo, claro e focado em resolver problemas técnicos. Se não tiver certeza da solução, sugira criar um chamado técnico." },
                        new { role = "user", content = prompt }
                    },
                    temperature = _temperature,
                    max_tokens = _maxTokens
                };

                var response = await _httpClient.PostAsJsonAsync("chat/completions", requestBody, ct);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>(ct);
                
                if (result?.choices == null || result.choices.Length == 0)
                {
                    throw new Exception("Resposta vazia da OpenAI");
                }

                var message = result.choices[0].message?.content ?? "Sem resposta";
                var usage = result.usage;

                _logger.LogInformation("OpenAI respondeu. Model: {Model}, Tokens: {Tokens}", _model, usage?.total_tokens ?? 0);

                return (
                    Provedor: "OpenAI",
                    Modelo: _model,
                    Mensagem: message,
                    Confianca: 0.95m,
                    ResumoRaciocinio: $"Resposta gerada por {_model}",
                    InputTokens: usage?.prompt_tokens,
                    outputTokens: usage?.completion_tokens,
                    CustoUsd: null
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao chamar OpenAI");
                throw new Exception($"Erro ao comunicar com OpenAI: {ex.Message}", ex);
            }
        }

        public async Task<string> ProcessarMensagemAsync(string mensagem, string sessionId, CancellationToken ct)
        {
            if (!EstaHabilitado())
            {
                throw new InvalidOperationException("OpenAI não está habilitado");
            }

            var resultado = await ResponderAsync(mensagem, ct);
            return resultado.Mensagem;
        }

        private class OpenAIResponse
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
