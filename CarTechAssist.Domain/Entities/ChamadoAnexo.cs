using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarTechAssist.Domain.Entities
{
    public class ChamadoAnexo  //core.ChamadoAnexo
    {
        public long AnexoId { get; set; }
        public long ChamadoId { get; set; }
        public long? InteracaoId { get; set; }
        public int TenantId { get; set; }
        public string NomeArquivo { get; set; } = null!;
        public string? ContentType { get; set; }
        public long? TamanhoBytes { get; set; }
        public byte[]? Conteudo { get; set; }
        public string? UrlExterna { get; set; }
        public byte[]? HashConteudo { get; set; }
        public DateTime DataCriacao { get; set; }
        public bool Excluido { get; set; }
        public byte[] RowVer { get; set; } = Array.Empty<byte>();


    }
}
