using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CarTechAssist.Domain.Enums;

namespace CarTechAssist.Domain.Entities
{
    public class ChamadoStatusHistorico  // log.ChamadoStatusHistorico
    {
        public long HistoricoId { get; set; }
        public long ChamadoId { get; set; }
        public int TenantId { get; set; }
        public StatusChamado? StatusAntigoId { get; set; }
        public StatusChamado StatusNovoId { get; set; }
        public int? AlteradoPorUsuarioId { get; set; }
        public string? Motivo { get; set; }
        public DateTime DataAlteracao { get; set; }

    }
}
