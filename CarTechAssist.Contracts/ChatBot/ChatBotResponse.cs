namespace CarTechAssist.Contracts.ChatBot
{
    public record ChatBotResponse(
        string Resposta,
        bool CriouChamado = false,
        long? ChamadoId = null,
        bool PrecisaEscalarParaHumano = false,
        string? SugestaoAcao = null,
        bool AguardandoConfirmacao = false,
        string? TipoConfirmacao = null,
        Dictionary<string, string>? Contexto = null,
        List<string>? Sugestoes = null
    );
}

