using System;

namespace CarTechAssist.Contracts.Usuarios
{
    public record AtualizarUsuarioRequest(
        string NomeCompleto,
        string? Email,
        string? Telefone,
        byte TipoUsuarioId
    );
}


