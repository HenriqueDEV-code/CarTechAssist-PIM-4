using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace CarTechAssist.Domain.Entities
{
    public class CategoriaChamado  // ref.CategoriaChamado
    {
        public int CategoriaId { get; set; }
        public int TenantId { get; set; }
        public string Nome { get; set; } = null!;
        public string? Codigo { get; set; }
        public int? CategoriaPaiId { get; set; }
        public bool Ativo { get; set; }
        public DateTime DataCriacao { get; set; }
        public DateTime? DataAtualizacao { get; set; }
        public bool Excluido { get; set; }
        public byte[] RowVer { get; set; } = Array.Empty<byte>();
    }
}
