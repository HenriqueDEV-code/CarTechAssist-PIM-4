namespace CarTechAssist.Contracts.Tickets
{
    public record AtribuirResponsavelRequest(
        int? ResponsavelUsuarioId,
        string? Motivo
    );
}

