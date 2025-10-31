namespace CarTechAssist.Contracts.Tickets
{
    public record CategoriaDto(
        int CategoriaId,
        string Nome,
        string? Codigo,
        int? CategoriaPaiId
    );
}

