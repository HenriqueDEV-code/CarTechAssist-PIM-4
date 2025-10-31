using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CarTechAssist.Contracts.Enums;    

namespace CarTechAssist.Contracts.Feedback
{
    public record FeedbackRequest (
    
            long ChamadoId,
            int TenantId,
            IAFeedbackScoreDto Score,
            string? Comentario
    );
}
