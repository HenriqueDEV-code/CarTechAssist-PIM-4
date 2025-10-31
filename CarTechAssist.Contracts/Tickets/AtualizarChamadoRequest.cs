namespace CarTechAssist.Contracts.Tickets
{
    public record AtualizarChamadoRequest(
        string? Titulo,
        string? Descricao,
        int? CategoriaId,
        byte? PrioridadeId,
        string? MotivoAlteracao
    );
}

