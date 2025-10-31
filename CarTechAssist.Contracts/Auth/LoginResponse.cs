namespace CarTechAssist.Contracts.Auth
{
    public record LoginResponse(
        string Token,
        string RefreshToken,
        int UsuarioId,
        string NomeCompleto,
        int TenantId,
        byte TipoUsuarioId
    );
}

