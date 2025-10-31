using System;

namespace CarTechAssist.Contracts.Usuarios
{
    public record CriarUsuarioRequest(
        string Login,
        string NomeCompleto,
        string? Email,
        string? Telefone,
        byte TipoUsuarioId,
        string Senha
    );
}


