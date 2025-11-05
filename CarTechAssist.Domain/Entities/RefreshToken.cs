namespace CarTechAssist.Domain.Entities
{
    public class RefreshToken
    {
        public long RefreshTokenId { get; set; }
        public int UsuarioId { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiraEm { get; set; }
        public bool Revogado { get; set; }
        public DateTime DataCriacao { get; set; }
        public DateTime? DataRevogacao { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }

        // Navigation property
        public Usuario? Usuario { get; set; }
    }
}

