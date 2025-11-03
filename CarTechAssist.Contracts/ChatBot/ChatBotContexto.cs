namespace CarTechAssist.Contracts.ChatBot
{
    public record ChatBotContexto(
        long? ChamadoId,
        string? TemaConversa,
        List<string> HistoricoMensagens,
        bool AguardandoConfirmacao,
        string? AcaoPendente,
        DateTime UltimaInteracao
    );
}

