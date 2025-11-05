using System;

namespace CarTechAssist.Contracts.Tickets
{
    public record ChamadoDetailDto(
        long ChamadoId,
        string Numero,
        string Titulo,
        string? Descricao,
        int? CategoriaId,
        byte StatusId,
        byte PrioridadeId,
        byte CanalId,
        int SolicitanteUsuarioId,
        int? ResponsavelUsuarioId,
        DateTime DataCriacao,
        DateTime? DataAtualizacao,
        string StatusNome, // CORREÇÃO: Nome legível do status
        string PrioridadeNome, // CORREÇÃO: Nome legível da prioridade
        string CanalNome // CORREÇÃO: Nome legível do canal
    );
}


