using System;

namespace CarTechAssist.Contracts.Usuarios
{
    public record UsuarioDto(
        int UsuarioId,
        string Login,
        string NomeCompleto,
        string? Email,
        string? Telefone,
        byte TipoUsuarioId,
        bool Ativo,
        DateTime DataCriacao
    );
}


