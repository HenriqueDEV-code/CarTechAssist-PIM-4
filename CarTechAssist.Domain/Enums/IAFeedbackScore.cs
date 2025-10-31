using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarTechAssist.Domain.Enums
{
    /// <summary>
    /// Representa a avaliação do atendimento ou resposta gerada pela IA.
    /// Usado em core.Chamado.IA_FeedbackScore e ia.IAFeedback.Score.
    /// Faixa válida: 0 (sem avaliação) a 5 (excelente).
    /// </summary>
    /// 
    public enum IAFeedbackScore : byte
    {
        NaoAvaliado = 0,
        MuitoRuim = 1,
        Ruim = 2,
        Regular = 3,
        Bom = 4,
        Excelente = 5
    }
}
