using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CarTechAssist.Domain.Enums;

namespace CarTechAssist.Domain.Entities
{
    public class ChamadoInteracao // core.ChamadoInteracao
    {
        public long InteracaoId { get; set; }
        public long ChamadoId { get; set; }
        public int TenantId { get; set; }
        public int? AutorUsuarioId { get; set; }
        public  TipoUsuarios AutorTipoUsuarioId { get; set; }
        public CanalAtendimento CanalId { get; set; }
        public string? Mensagem { get; set; }
        public bool Interna { get; set; }

        public bool IA_Gerada { get; set; }
        public string? IA_Modelo { get; set; }
        public decimal? IA_Confianca { get; set; }
        public string? IA_ResumoRaciocinio { get; set; }
        public DateTime DataCriacao { get; set; }
        public bool Excluido { get; set; }
        public byte[] RowVer { get; set; } = Array.Empty<byte>();

    }
}
