namespace CarTechAssist.Contracts.Auth
{
    public record SolicitarRecuperacaoRequest(
        string Login,
        string Email
    );
}

