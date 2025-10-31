using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CarTechAssist.Domain.Enums;

namespace CarTechAssist.Domain.Entities
{
    public class IAFeedback // ia.IAFeedback
    {
        public long FeedbackId { get; set; }
        public int TenantId { get; set; }
        public long? ChamadoId { get; set; }
        public long? InteracaoId { get; set; }
        public int? DadoPorUsuarioId { get; set; }
        public IAFeedbackScore Score { get; set; }        // 0..5
        public string? Comentario { get; set; }
        public DateTime DataCriacao { get; set; }

    }
}
