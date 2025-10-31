using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarTechAssist.Domain.Entities
{
    public class ApiClient // sec.ApiClient
    {
        public long AuditoriaId { get; set; }
        public int TenantId { get; set; }
        public string Entidade { get; set; } = null!;
        public string EntidadeId { get; set; } = null!;
        public string Acao { get; set; } = null!;
        public int? AlteradoPorUsuarioId { get; set; }
        public Guid? CorrelationId { get; set; }
        public string? PatchJson { get; set; }
        public DateTime DataCriacao { get; set; }
    }
}
