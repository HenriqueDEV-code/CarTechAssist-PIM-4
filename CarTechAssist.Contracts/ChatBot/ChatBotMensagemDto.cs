namespace CarTechAssist.Contracts.ChatBot
{
    public record ChatBotMensagemDto(
        long InteracaoId,
        string Mensagem,
        bool EhBot,
        DateTime DataCriacao,
        long? ChamadoId
    );
}

