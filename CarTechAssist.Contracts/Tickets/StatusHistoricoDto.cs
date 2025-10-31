using System;

namespace CarTechAssist.Contracts.Tickets
{
    public record StatusHistoricoDto(
        long HistoricoId,
        long ChamadoId,
        string? StatusAntigo,
        string StatusNovo,
        int? AlteradoPorUsuarioId,
        string? AlteradoPorNome,
        string? Motivo,
        DateTime DataAlteracao
    );
}
