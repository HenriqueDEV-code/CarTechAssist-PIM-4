using CarTechAssist.Domain.Interfaces;
using Google.Cloud.Dialogflow.V2;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CarTechAssist.Application.Services
{
    public class DialogflowService : IAiProvider
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DialogflowService> _logger;
        private readonly SessionsClient? _sessionsClient;
        private readonly bool _enabled;
        private readonly string _projectId;
        private readonly string _languageCode;

        public DialogflowService(IConfiguration configuration, ILogger<DialogflowService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            _enabled = bool.Parse(_configuration["Dialogflow:Enabled"] ?? "false");
            _projectId = _configuration["Dialogflow:ProjectId"] ?? string.Empty;
            _languageCode = _configuration["Dialogflow:LanguageCode"] ?? "pt-BR";

            if (_enabled && !string.IsNullOrEmpty(_projectId))
            {
                try
                {
                    var jsonCredentials = _configuration["Dialogflow:JsonCredentials"];
                    if (!string.IsNullOrEmpty(jsonCredentials))
                    {
                        // Se for base64, decodificar; caso contrário, tratar como caminho de arquivo
                        byte[] credentialsBytes;
                        try
                        {
                            // Tentar decodificar como base64
                            credentialsBytes = Convert.FromBase64String(jsonCredentials);
                        }
                        catch
                        {
                            // Se falhar, tratar como caminho de arquivo
                            if (File.Exists(jsonCredentials))
                            {
                                credentialsBytes = File.ReadAllBytes(jsonCredentials);
                            }
                            else
                            {
                                _logger.LogWarning("Credenciais do Dialogflow não encontradas no caminho: {Path}", jsonCredentials);
                                return;
                            }
                        }

                        // Criar arquivo temporário com as credenciais
                        var tempFile = Path.GetTempFileName();
                        File.WriteAllBytes(tempFile, credentialsBytes);

                        // Configurar variável de ambiente para Google Cloud
                        System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", tempFile);

                        _sessionsClient = SessionsClient.Create();
                        _logger.LogInformation("DialogflowService configurado com sucesso. ProjectId: {ProjectId}", _projectId);
                    }
                    else
                    {
                        _logger.LogWarning("Dialogflow:JsonCredentials não configurado no appsettings.json");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao configurar DialogflowService");
                }
            }
            else
            {
                _logger.LogInformation("Dialogflow está desabilitado ou ProjectId não configurado");
            }
        }

        public async Task<(string Provedor, string Modelo, string Mensagem, decimal? Confianca, string? ResumoRaciocinio, int? InputTokens, int? outputTokens, decimal? CustoUsd)> ResponderAsync(string prompt, CancellationToken ct)
        {
            if (!_enabled || _sessionsClient == null || string.IsNullOrEmpty(_projectId))
            {
                throw new InvalidOperationException("Dialogflow não está habilitado ou configurado corretamente.");
            }

            try
            {
                // Criar session ID único baseado no tenant/usuário ou usar um genérico
                var sessionId = $"session_{Guid.NewGuid():N}";
                var session = new SessionName(_projectId, sessionId);

                var queryInput = new QueryInput
                {
                    Text = new TextInput
                    {
                        Text = prompt,
                        LanguageCode = _languageCode
                    }
                };

                _logger.LogInformation("Enviando mensagem para Dialogflow. Session: {Session}, Prompt: {Prompt}", session, prompt);

                var response = await _sessionsClient.DetectIntentAsync(
                    new DetectIntentRequest
                    {
                        SessionAsSessionName = session,
                        QueryInput = queryInput
                    },
                    cancellationToken: ct);

                var intent = response.QueryResult.Intent;
                var fulfillmentText = response.QueryResult.FulfillmentText;
                var confidence = response.QueryResult.IntentDetectionConfidence;

                _logger.LogInformation("Dialogflow respondeu. Intent: {Intent}, Confidence: {Confidence}, FulfillmentText: {FulfillmentText}",
                    intent?.DisplayName, confidence, fulfillmentText);

                return (
                    Provedor: "Dialogflow",
                    Modelo: intent?.DisplayName ?? "unknown",
                    Mensagem: fulfillmentText,
                    Confianca: (decimal?)confidence,
                    ResumoRaciocinio: $"Intent detectado: {intent?.DisplayName}",
                    InputTokens: null, // Dialogflow não fornece tokens diretamente
                    outputTokens: null,
                    CustoUsd: null
                );
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Erro RPC ao chamar Dialogflow: {Status}", ex.Status);
                throw new Exception($"Erro ao comunicar com Dialogflow: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar mensagem no Dialogflow");
                throw;
            }
        }

        public async Task<string> ProcessarMensagemAsync(string mensagem, string sessionId, CancellationToken ct)
        {
            if (!_enabled || _sessionsClient == null || string.IsNullOrEmpty(_projectId))
            {
                return "Dialogflow não está disponível no momento.";
            }

            try
            {
                var session = new SessionName(_projectId, sessionId);

                var queryInput = new QueryInput
                {
                    Text = new TextInput
                    {
                        Text = mensagem,
                        LanguageCode = _languageCode
                    }
                };

                var response = await _sessionsClient.DetectIntentAsync(
                    new DetectIntentRequest
                    {
                        SessionAsSessionName = session,
                        QueryInput = queryInput
                    },
                    cancellationToken: ct);

                return response.QueryResult.FulfillmentText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar mensagem no Dialogflow");
                return "Desculpe, ocorreu um erro ao processar sua mensagem. Tente novamente.";
            }
        }

        public bool EstaHabilitado() => _enabled && _sessionsClient != null;
    }
}

