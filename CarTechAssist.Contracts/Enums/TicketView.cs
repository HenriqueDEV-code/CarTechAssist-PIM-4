using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CarTechAssist.Contracts.Enums;

namespace CarTechAssist.Contracts.Enums
{
   public record TicketView(


       long ChamadoId,
       string Numero,
       string Titulo,
       string StatusNome,
       string PrioridadeNome,
       IAFeedbackScoreDto? feedback,
       DateTime DataCriacao


    );
}
