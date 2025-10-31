using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CarTechAssist.Domain.Enums;

namespace CarTechAssist.Domain.Entities
{
    public class Usuario // core.Usuario
    {
        public int UsuarioId { get; set; }
        public int TenantId { get; set; }
        public TipoUsuarios TipoUsuarioId { get; set; }
        public string Login { get; set; } = null!;
        public string NomeCompleto { get; set; } = null!;
        public string? Email { get; set; }
        public string? Telefone { get; set; }
        public byte[]? HashSenha { get; set; }
        public byte[]? SaltSenha { get; set; }
        public bool PrecisaTrocarSenha { get; set; }
        public bool Ativo { get; set; } = true;
        public DateTime DataCriacao { get; set; }
        public DateTime? DataAtualizacao { get; set; }
        public bool Excluido { get; set; } = false;
        public byte[]? RowVersion { get; set; } = Array.Empty<byte>();

    }
}
