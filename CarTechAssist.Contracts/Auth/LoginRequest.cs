namespace CarTechAssist.Contracts.Auth
{
    public record LoginRequest(
        string Login,
        string Senha,
        int TenantId
    );
}

