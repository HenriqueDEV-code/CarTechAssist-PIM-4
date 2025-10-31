using System;

namespace CarTechAssist.Contracts.Tickets
{
    public record CriarChamadoRequest(
        string Titulo,
        string? Descricao,
        int? CategoriaId,
        byte PrioridadeId,
        byte CanalId,
        int SolicitanteUsuarioId,
        int? ResponsavelUsuarioId
    );
}


