namespace CarTechAssist.Contracts.Tickets
{
    public record InteracaoDto(
        long InteracaoId,
        long ChamadoId,
        int? AutorUsuarioId,
        string? AutorNome,
        byte AutorTipoUsuarioId,
        byte CanalId,
        string? Mensagem,
        bool Interna,
        bool IA_Gerada,
        string? IA_Modelo,
        decimal? IA_Confianca,
        string? IA_ResumoRaciocinio,
        DateTime DataCriacao
    );
}

