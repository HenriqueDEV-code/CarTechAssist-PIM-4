using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarTechAssist.Domain.Entities
{
    public class Tenant // Ref.Tenant
    {
        public int TenantId { get; set; }
        public string Nome { get; set; } = null!;
        public string Codigo { get; set; } = null!;
        public bool Ativo { get; set; } = true;
        public DateTime DataCriacao { get; set; }
        public DateTime? DataAtualizacao { get; set; }
        public bool Excluido { get; set; } = false;
        public byte[] RowVer { get; set; } = Array.Empty<byte>();
    }
}
