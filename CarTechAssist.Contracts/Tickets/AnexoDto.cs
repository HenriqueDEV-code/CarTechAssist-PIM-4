namespace CarTechAssist.Contracts.Tickets
{
    public record AnexoDto(
        long AnexoId,
        long ChamadoId,
        long? InteracaoId,
        string NomeArquivo,
        string? ContentType,
        long? TamanhoBytes,
        string? UrlExterna,
        DateTime DataCriacao
    );
}

