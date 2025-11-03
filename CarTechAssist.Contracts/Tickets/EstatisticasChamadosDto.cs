namespace CarTechAssist.Contracts.Tickets
{
    public record EstatisticasChamadosDto(
        int Total,
        int Abertos,
        int EmAndamento,
        int Resolvidos,
        int Cancelados,
        int PorUrgenciaAlta,
        int PorUrgenciaMedia,
        int PorUrgenciaBaixa
    );
}

