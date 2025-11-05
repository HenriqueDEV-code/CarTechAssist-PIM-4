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
       DateTime DataCriacao,
       string CanalNome, // CORREÇÃO: Nome do canal
       int? CategoriaId, // CORREÇÃO: ID da categoria
       string? DescricaoResumida, // CORREÇÃO: Descrição resumida para listagem
       int SolicitanteUsuarioId, // CORREÇÃO: ID do solicitante
       int? ResponsavelUsuarioId // CORREÇÃO: ID do responsável
    );
}
