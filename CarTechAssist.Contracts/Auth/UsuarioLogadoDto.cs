namespace CarTechAssist.Contracts.Auth
{
    public record UsuarioLogadoDto(
        int UsuarioId,
        int TenantId,
        string Login,
        string NomeCompleto,
        string? Email,
        string? Telefone,
        byte TipoUsuarioId,
        string TipoUsuarioNome
    );
}

