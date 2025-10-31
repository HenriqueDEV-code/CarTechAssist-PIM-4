namespace CarTechAssist.Domain.Entities
{
    public class RecuperacaoSenha
    {
        public long RecuperacaoSenhaId { get; set; }
        public int TenantId { get; set; }
        public int UsuarioId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime DataExpiracao { get; set; }
        public bool Usado { get; set; }
        public DateTime DataCriacao { get; set; }
        public DateTime? DataUso { get; set; }
    }
}

