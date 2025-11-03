namespace CarTechAssist.Contracts.ChatBot
{
    public record ChatBotRequest(
        string Mensagem,
        long? ChamadoId
    );
}

