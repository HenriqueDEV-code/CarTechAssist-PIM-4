namespace CarTechAssist.Contracts.Auth
{
    public record RedefinirSenhaRequest(
        string Codigo,
        string NovaSenha
    );
}

