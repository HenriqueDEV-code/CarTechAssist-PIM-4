using CarTechAssist.Contracts.ChatBot;

namespace CarTechAssist.Web.Services
{
    public class ChatBotService
    {
        private readonly ApiClientService _apiClient;
        private readonly ILogger<ChatBotService> _logger;

        public ChatBotService(ApiClientService apiClient, ILogger<ChatBotService> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        public async Task<ChatBotResponse?> EnviarMensagemAsync(
            string mensagem,
            long? chamadoId = null,
            CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(mensagem))
                {
                    _logger.LogWarning("Tentativa de enviar mensagem vazia para ChatBot");
                    throw new ArgumentException("A mensagem não pode estar vazia.");
                }

                var request = new ChatBotRequest(
                    Mensagem: mensagem.Trim(),
                    ChamadoId: chamadoId
                );

                _logger.LogDebug("Enviando requisição para ChatBot API. MensagemLength: {Length}, ChamadoId: {ChamadoId}", 
                    mensagem.Length, chamadoId);

                var resposta = await _apiClient.PostAsync<ChatBotResponse>("api/chatbot/mensagem", request, ct);

                if (resposta == null)
                {
                    _logger.LogWarning("API ChatBot retornou resposta nula");
                }
                else
                {
                    _logger.LogDebug("ChatBot API retornou resposta. CriouChamado: {CriouChamado}", resposta.CriouChamado);
                }

                return resposta;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar mensagem para ChatBot API. Mensagem: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<IReadOnlyList<ChatBotMensagemDto>?> ObterHistoricoAsync(
            long chamadoId,
            CancellationToken ct = default)
        {
            return await _apiClient.GetAsync<IReadOnlyList<ChatBotMensagemDto>>(
                $"api/chatbot/historico/{chamadoId}", ct);
        }
    }
}

