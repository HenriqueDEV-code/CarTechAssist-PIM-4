using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CarTechAssist.Domain.Enums;

namespace CarTechAssist.Domain.Entities
{
    public class Chamado // core.Chamado
    {
        public long ChamadoId { get; set; }
        public int TenantId { get; set; }
        public string Numero { get; set; } = null!;   // computed no SQL (YYYY-XXXXXX)
        public string Titulo { get; set; } = null!;
        public string? Descricao { get; set; }
        public int? CategoriaId { get; set; }
        public StatusChamado StatusId { get; set; }
        public PrioridadeChamado PrioridadeId { get; set; }
        public CanalAtendimento CanalId { get; set; }
        public int SolicitanteUsuarioId { get; set; }
        public int? ResponsavelUsuarioId { get; set; }
        public DateTime? SLA_EstimadoFim { get; set; }
        public DateTime? DataResolvido { get; set; }
        public DateTime? DataFechado { get; set; }

        public int? IA_CategoriaSugeridaId { get; set; }
        public decimal? IA_PrioridadeScore { get; set; }
        public string? IA_Resumo { get; set; }
        public string? IA_UltimoModelo { get; set; }
        public decimal? IA_UltimaConfianca { get; set; }
        public bool IA_AtendidoPorIA { get; set; }
        public IAFeedbackScore? IA_FeedbackScore { get; set; }

        public string? FonteExternaId { get; set; }
        public DateTime DataCriacao { get; set; }
        public DateTime? DataAtualizacao { get; set; }
        public bool Excluido { get; set; }
        public byte[] RowVer { get; set; } = Array.Empty<byte>();
    }
}
