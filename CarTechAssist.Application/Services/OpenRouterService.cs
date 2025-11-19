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
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly bool _enabled;
        private readonly string _model;
        private readonly int _maxTokens;
        private readonly double _temperature;
        private readonly string? _apiKey;

        public OpenRouterService(IConfiguration configuration, ILogger<OpenRouterService> logger, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _enabled = bool.Parse(_configuration["OpenRouter:Enabled"] ?? "false");
            _model = _configuration["OpenRouter:Model"] ?? "openai/gpt-4o-mini";
            _maxTokens = int.Parse(_configuration["OpenRouter:MaxTokens"] ?? "1000");
            
            // Parse temperature usando InvariantCulture para garantir que 0.7 não vire 7
            var temperatureStr = _configuration["OpenRouter:Temperature"] ?? "0.7";
            _temperature = double.Parse(temperatureStr, System.Globalization.CultureInfo.InvariantCulture);
            
            // Validar que temperature está entre 0 e 2 (limite do OpenRouter)
            if (_temperature < 0 || _temperature > 2)
            {
                _logger.LogWarning("⚠️ Temperature {Temperature} está fora do range válido (0-2). Ajustando para 0.7", _temperature);
                _temperature = 0.7;
            }
            
            _apiKey = _configuration["OpenRouter:ApiKey"]?.Trim(); // Remover espaços em branco

            _logger.LogInformation("🔍 Configuração OpenRouter carregada:");
            _logger.LogInformation("   Enabled: {Enabled}", _enabled);
            _logger.LogInformation("   Model: {Model}", _model);
            _logger.LogInformation("   MaxTokens: {MaxTokens}", _maxTokens);
            _logger.LogInformation("   Temperature: {Temperature}", _temperature);
            _logger.LogInformation("   ApiKey presente: {HasApiKey}, Tamanho: {Length}", 
                !string.IsNullOrEmpty(_apiKey), _apiKey?.Length ?? 0);
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogInformation("   ApiKey prefixo: {Prefix}...{Suffix}", 
                    _apiKey.Substring(0, Math.Min(20, _apiKey.Length)),
                    _apiKey.Length > 20 ? _apiKey.Substring(_apiKey.Length - 4) : "");
            }

            if (_enabled)
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    _logger.LogError("❌ OpenRouter habilitado mas ApiKey não configurada ou vazia!");
                }
                else if (!_apiKey.StartsWith("sk-or-v1-"))
                {
                    _logger.LogError("❌ API Key do OpenRouter não está no formato correto! Deve começar com 'sk-or-v1-'");
                    _logger.LogError("   API Key atual começa com: {Prefix}", 
                        _apiKey.Length > 10 ? _apiKey.Substring(0, 10) : _apiKey);
                }
                else
                {
                    _logger.LogInformation("✅ OpenRouterService configurado com sucesso!");
                    _logger.LogInformation("   API Base: https://openrouter.ai/api/v1/");
                }
            }
        }

        public bool EstaHabilitado() => _enabled && !string.IsNullOrEmpty(_apiKey) && _apiKey.StartsWith("sk-or-v1-");

        private HttpClient CriarHttpClient()
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
            client.Timeout = TimeSpan.FromSeconds(30);
            
            // Não adicionar headers aqui - serão adicionados diretamente na requisição
            // Isso evita problemas com headers persistentes e garante controle total
            
            _logger.LogInformation("🔧 HttpClient criado:");
            _logger.LogInformation("   BaseAddress: {BaseAddress}", client.BaseAddress);
            _logger.LogInformation("   Timeout: {Timeout} segundos", client.Timeout.TotalSeconds);
            
            return client;
        }

        public async Task<(string Provedor, string Modelo, string Mensagem, decimal? Confianca, string? ResumoRaciocinio, int? InputTokens, int? outputTokens, decimal? CustoUsd)> ResponderAsync(string prompt, CancellationToken ct)
        {
            if (!EstaHabilitado())
            {
                throw new InvalidOperationException("OpenRouter não está habilitado ou a API Key não está configurada corretamente.");
            }

            HttpClient? client = null;
            try
            {
                client = CriarHttpClient();
                _logger.LogInformation("📤 Enviando prompt para OpenRouter. Model: {Model}, PromptLength: {Length}", _model, prompt.Length);

                var systemPrompt = @"Você é um assistente de suporte técnico do CarTechAssist, responsável por atender chamados abertos por clientes antes que cheguem a um agente humano.

Seu objetivo é:
- Entender o problema do cliente
- Tentar resolver de forma autônoma sempre que possível
- Manter uma conversa clara, educada e objetiva
- Atualizar o status do chamado conforme o andamento
- Encaminhar para um agente humano quando necessário
- Criar novos chamados relacionados, quando fizer sentido

DIRETRIZES:
1. Seja objetivo, claro, educado e profissional
2. Tente resolver problemas simples com instruções diretas
3. Se o problema for complexo ou não tiver solução imediata, encaminhe para um agente humano
4. Use linguagem técnica mas acessível
5. Quando usar termos técnicos, explique de forma simples
6. Evite jargões técnicos sem explicação

ATUALIZAÇÃO DE STATUS:
- Use [STATUS:2] para encaminhar para agente (Em Andamento)
- Use [STATUS:3] para marcar como pendente (aguardando resposta do cliente)
- Use [STATUS:5] para fechar o chamado quando resolvido

CRIAÇÃO DE NOVOS CHAMADOS:
- Se surgir outra demanda relacionada, use [NOVO_CHAMADO:Título|Descrição|CategoriaId|PrioridadeId]

SEMPRE mantenha o cliente informado sobre o que você está fazendo.";

                // Garantir que temperature está no range válido (0-2) antes de enviar
                var temperatureToSend = Math.Max(0, Math.Min(2, _temperature));
                
                if (temperatureToSend != _temperature)
                {
                    _logger.LogWarning("⚠️ Temperature ajustado de {Original} para {Ajustado} (range válido: 0-2)", 
                        _temperature, temperatureToSend);
                }
                
                var requestBody = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = prompt }
                    },
                    temperature = temperatureToSend,
                    max_tokens = _maxTokens
                };
                
                _logger.LogInformation("📋 Request body preparado:");
                _logger.LogInformation("   Model: {Model}", _model);
                _logger.LogInformation("   Temperature: {Temperature} (validado: 0-2)", temperatureToSend);
                _logger.LogInformation("   MaxTokens: {MaxTokens}", _maxTokens);

                var requestUrl = client.BaseAddress + "chat/completions";
                _logger.LogInformation("🔍 Enviando requisição para OpenRouter:");
                _logger.LogInformation("   URL: {Url}", requestUrl);
                _logger.LogInformation("   Model: {Model}", _model);
                _logger.LogInformation("   Temperature: {Temperature}", _temperature);
                _logger.LogInformation("   MaxTokens: {MaxTokens}", _maxTokens);
                _logger.LogInformation("   Prompt length: {Length} caracteres", prompt.Length);
                
                // Verificar headers antes de enviar
                // Log da API Key antes de criar a requisição
                if (!string.IsNullOrWhiteSpace(_apiKey))
                {
                    var apiKeyTrimmed = _apiKey.Trim();
                    _logger.LogInformation("🔐 API Key que será usada:");
                    _logger.LogInformation("   Tamanho: {Length} caracteres", apiKeyTrimmed.Length);
                    _logger.LogInformation("   Prefixo: {Prefix}", apiKeyTrimmed.Substring(0, Math.Min(15, apiKeyTrimmed.Length)));
                    _logger.LogInformation("   Sufixo: ...{Suffix}", apiKeyTrimmed.Length > 15 ? apiKeyTrimmed.Substring(apiKeyTrimmed.Length - 4) : "");
                    _logger.LogInformation("   Começa com 'sk-or-v1-': {StartsWith}", apiKeyTrimmed.StartsWith("sk-or-v1-"));
                }
                else
                {
                    _logger.LogError("❌ API Key está vazia!");
                }

                // Usar HttpRequestMessage diretamente para ter controle total sobre os headers
                var jsonContent = System.Text.Json.JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                
                var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
                {
                    Content = content
                };
                
                // Adicionar headers diretamente na requisição (não nos DefaultRequestHeaders)
                if (!string.IsNullOrWhiteSpace(_apiKey))
                {
                    request.Headers.Add("Authorization", $"Bearer {_apiKey.Trim()}");
                }
                
                var httpReferer = _configuration["OpenRouter:HttpReferer"] ?? "https://cartechassist.local";
                request.Headers.Add("HTTP-Referer", httpReferer);
                request.Headers.Add("X-Title", "CarTechAssist");
                
                _logger.LogInformation("📤 Enviando HttpRequestMessage:");
                _logger.LogInformation("   Method: {Method}", request.Method);
                _logger.LogInformation("   RequestUri: {Uri}", request.RequestUri);
                _logger.LogInformation("   Headers Authorization presente: {HasAuth}", request.Headers.Contains("Authorization"));
                if (request.Headers.Contains("Authorization"))
                {
                    var authValues = request.Headers.GetValues("Authorization").ToList();
                    _logger.LogInformation("   Authorization header valores: {Count}", authValues.Count);
                    foreach (var authValue in authValues)
                    {
                        _logger.LogInformation("   Authorization: {Prefix}...{Suffix} (tamanho: {Length})", 
                            authValue.Substring(0, Math.Min(25, authValue.Length)),
                            authValue.Length > 25 ? authValue.Substring(authValue.Length - 4) : "",
                            authValue.Length);
                    }
                }
                
                var response = await client.SendAsync(request, ct);
                
                _logger.LogInformation("📥 Resposta recebida do OpenRouter:");
                _logger.LogInformation("   Status Code: {StatusCode} ({StatusName})", (int)response.StatusCode, response.StatusCode);
                _logger.LogInformation("   Reason Phrase: {ReasonPhrase}", response.ReasonPhrase);
                _logger.LogInformation("   Headers da resposta: {Headers}", string.Join(", ", response.Headers.Select(h => $"{h.Key}={string.Join(",", h.Value)}")));
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogError("❌ OpenRouter retornou erro:");
                    _logger.LogError("   Status Code: {StatusCode} ({StatusName})", (int)response.StatusCode, response.StatusCode);
                    _logger.LogError("   Reason Phrase: {ReasonPhrase}", response.ReasonPhrase);
                    _logger.LogError("   Response Body completo: {Response}", errorContent);
                    
                    // Tentar parsear o JSON de erro para extrair mais detalhes
                    try
                    {
                        var errorJson = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(errorContent);
                        if (errorJson.TryGetProperty("error", out var errorObj))
                        {
                            if (errorObj.TryGetProperty("message", out var messageProp))
                            {
                                _logger.LogError("   Mensagem de erro do OpenRouter: {Message}", messageProp.GetString());
                            }
                            if (errorObj.TryGetProperty("code", out var codeProp))
                            {
                                _logger.LogError("   Código de erro do OpenRouter: {Code}", codeProp.GetString());
                            }
                        }
                    }
                    catch (Exception parseEx)
                    {
                        _logger.LogWarning("   Não foi possível parsear o JSON de erro: {Error}", parseEx.Message);
                    }
                    
                    throw new HttpRequestException(
                        $"OpenRouter retornou erro {response.StatusCode}: {response.ReasonPhrase}. Response: {errorContent}",
                        null,
                        response.StatusCode);
                }

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
            catch (HttpRequestException httpEx)
            {
                var statusCode = httpEx.Data.Contains("StatusCode") ? httpEx.Data["StatusCode"]?.ToString() : "Desconhecido";
                
                _logger.LogError(httpEx, "❌ Erro HTTP ao chamar OpenRouter:");
                _logger.LogError("   Tipo de exceção: {Type}", httpEx.GetType().Name);
                _logger.LogError("   Status Code: {StatusCode}", statusCode);
                _logger.LogError("   Mensagem: {Message}", httpEx.Message);
                _logger.LogError("   Stack Trace: {StackTrace}", httpEx.StackTrace);
                
                if (httpEx.InnerException != null)
                {
                    _logger.LogError("   Inner Exception: {InnerType} - {InnerMessage}", 
                        httpEx.InnerException.GetType().Name, httpEx.InnerException.Message);
                }
                
                // Log detalhado da API Key para debug (sem expor completamente)
                if (!string.IsNullOrEmpty(_apiKey))
                {
                    _logger.LogError("   API Key usada: {Prefix}...{Suffix} (tamanho: {Length} caracteres)", 
                        _apiKey.Substring(0, Math.Min(15, _apiKey.Length)),
                        _apiKey.Length > 15 ? _apiKey.Substring(_apiKey.Length - 4) : "",
                        _apiKey.Length);
                    _logger.LogError("   API Key começa com 'sk-or-v1-': {StartsWith}", _apiKey.StartsWith("sk-or-v1-"));
                    _logger.LogError("   API Key contém espaços: {HasSpaces}", _apiKey.Contains(" "));
                    _logger.LogError("   API Key contém quebras de linha: {HasNewlines}", _apiKey.Contains("\n") || _apiKey.Contains("\r"));
                }
                else
                {
                    _logger.LogError("   API Key: NULA ou VAZIA!");
                }
                
                // Mensagem mais clara para o usuário
                if (statusCode?.ToString() == "401" || httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized") || httpEx.Message.Contains("User not found"))
                {
                    var detalhes = $"Status: {statusCode}, Mensagem: {httpEx.Message}";
                    if (httpEx.InnerException != null)
                    {
                        detalhes += $", InnerException: {httpEx.InnerException.Message}";
                    }
                    
                    var apiKeyInfo = "NÃO CONFIGURADA";
                    if (!string.IsNullOrEmpty(_apiKey))
                    {
                        apiKeyInfo = $"{_apiKey.Substring(0, Math.Min(15, _apiKey.Length))}...{(_apiKey.Length > 15 ? _apiKey.Substring(_apiKey.Length - 4) : "")} (tamanho: {_apiKey.Length})";
                        if (!_apiKey.StartsWith("sk-or-v1-"))
                        {
                            apiKeyInfo += " - FORMATO INCORRETO (deve começar com 'sk-or-v1-')";
                        }
                    }
                    
                    throw new InvalidOperationException(
                        $"Erro de autenticação com OpenRouter ({detalhes}). " +
                        $"Verifique se a API Key no appsettings.json está correta, ativa e começa com 'sk-or-v1-'. " +
                        $"Acesse https://openrouter.ai/keys para verificar ou criar uma nova API Key. " +
                        $"API Key atual: {apiKeyInfo}", 
                        httpEx);
                }
                
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro inesperado ao chamar OpenRouter. Tipo: {Tipo}, Message: {Message}, InnerException: {InnerException}", 
                    ex.GetType().Name, ex.Message, ex.InnerException?.Message);
                throw new Exception($"Erro ao comunicar com OpenRouter: {ex.Message}", ex);
            }
            finally
            {
                client?.Dispose();
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

