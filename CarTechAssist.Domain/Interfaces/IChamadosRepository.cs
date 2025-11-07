using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CarTechAssist.Domain.Entities;

namespace CarTechAssist.Domain.Interfaces
{
    public interface IChamadosRepository
    {

        Task<Chamado?> ObterAsync(long chamadoId, CancellationToken ct);

        Task<(IReadOnlyList<Chamado> Items, int Total)> ListaAsync(
              int tenantId, 
              byte? statusId,
              int? responsaveUsuarioId,
              int? solicitanteUsuarioId, 
              int page, 
              int pageSize,
              CancellationToken ct
            );


        Task<Chamado> CriarAsync(
              int tenantId,
              string titulo,
              string? descricao,
              int? categoriaId,
              byte prioridadeId,
              byte canalId,
              int solicitanteUsuarioId,
              int? responsavelUsuarioId,
              DateTime? slaEstimadoFim,
              CancellationToken ct
            );

        Task<Chamado> AdicionarInteracaoIaAsync(
            long chamadoId,
            int tenantId,
            string modelo,
            string mensagem,
            decimal? confianca,
            string? resumoRaciocinio,
            string provedor,
            int? inputTokens,
            int? outputTokens,
            decimal? custoUsd,
            CancellationToken ct
            );

        Task<Chamado> AdicionarInteracaoAsync(
            long chamadoId,
            int tenantId,
            int usuarioId,
            string mensagem,
            CancellationToken ct);

        Task<IReadOnlyList<ChamadoInteracao>> ListarInteracoesAsync(
            long chamadoId,
            int tenantId,
            CancellationToken ct);

        Task<Chamado> AlterarStatusAsync(
            long chamadoId,
            int tenantId,
            byte novoStatus,
            int usuarioId,
            CancellationToken ct);

        Task AdicionarAnexoAsync(
            long chamadoId,
            int tenantId,
            string nomeArquivo,
            string contentType,
            byte[] bytes,
            int usuarioId,
            CancellationToken ct);

        Task<(int Total, int Abertos, int EmAndamento, int Resolvidos, int Cancelados)> ObterEstatisticasAsync(
            int tenantId,
            int? solicitanteUsuarioId,
            CancellationToken ct);
    }
}
