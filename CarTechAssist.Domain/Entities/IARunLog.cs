using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarTechAssist.Domain.Entities
{
    public class IARunLog // ia.IARunLog
    {
        public long IARunId { get; set; }
        public int TenantId { get; set; }
        public long? ChamadoId { get; set; }
        public long? InteracaoId { get; set; }
        public string Provedor { get; set; } = null!;
        public string Modelo { get; set; } = null!;
        public byte[]? PromptHash { get; set; }
        public int? InputTokens { get; set; }
        public int? OutputTokens { get; set; }
        public int? LatenciaMs { get; set; }
        public decimal? CustoUSD { get; set; }
        public decimal? Confianca { get; set; }
        public string? TipoResultado { get; set; }
        public DateTime DataCriacao { get; set; }


    }
}
