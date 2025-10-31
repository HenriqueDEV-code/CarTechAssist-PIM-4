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

        // SELECT * FROM core.vw_chamados WHERE id_chamado = @id_chamado
        Task<Chamado?> ObterAsync(long chamadoId, CancellationToken ct);


        // SELECT paginando da vw para listagens
        Task<(IReadOnlyList<Chamado> Items, int Total)> ListaAsync(
              int tenantId, 
              byte? statusId,
              int? responsaveUsuarioId,
              int? solicitanteUsuarioId, // Novo parâmetro
              int page, 
              int pageSize,
              CancellationToken ct
            );

        // EXEC core.usp_Chamado_Criar ... -> retorna registro da vw

        Task<Chamado> CriarAsync(
              int tenantId,
              string titulo,
              string? descricao,
              int? categoriaId,
              byte prioridadeId,
              byte canalId,
              int solicitanteUsuarioId,
              int? responsavelUsuarioId,
              CancellationToken ct
            );

        // EXEC core.usp_Chamado_IA_AdicionarInteracao
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

        // EXEC core.usp_Chamado_AdicionarInteracao
        Task<Chamado> AdicionarInteracaoAsync(
            long chamadoId,
            int tenantId,
            int usuarioId,
            string mensagem,
            CancellationToken ct);

        // EXEC core.usp_Chamado_AlterarStatus
        Task<Chamado> AlterarStatusAsync(
            long chamadoId,
            int tenantId,
            byte novoStatus,
            int usuarioId,
            CancellationToken ct);

        // EXEC core.usp_Chamado_AdicionarAnexo
        Task AdicionarAnexoAsync(
            long chamadoId,
            int tenantId,
            string nomeArquivo,
            string contentType,
            byte[] bytes,
            int usuarioId,
            CancellationToken ct);

        // Adicionar feedback de IA
        Task<Chamado> AdicionarFeedbackAsync(
            long chamadoId,
            int tenantId,
            int? usuarioId,
            byte score,
            string? comentario,
            CancellationToken ct);

        // Listar interações do chamado
        Task<IReadOnlyList<ChamadoInteracao>> ListarInteracoesAsync(
            long chamadoId,
            int tenantId,
            CancellationToken ct);

        // Listar interações do chamado com nome do autor (otimizado - JOIN)
        Task<IReadOnlyList<(ChamadoInteracao Interacao, string? AutorNome)>> ListarInteracoesComAutorAsync(
            long chamadoId,
            int tenantId,
            CancellationToken ct);

        // Obter anexo por ID
        Task<ChamadoAnexo?> ObterAnexoAsync(
            long anexoId,
            int tenantId,
            CancellationToken ct);

        // Listar anexos do chamado
        Task<IReadOnlyList<ChamadoAnexo>> ListarAnexosAsync(
            long chamadoId,
            int tenantId,
            CancellationToken ct);

        // Atualizar chamado
        Task<Chamado> AtualizarAsync(
            long chamadoId,
            int tenantId,
            string? titulo,
            string? descricao,
            int? categoriaId,
            byte? prioridadeId,
            int usuarioId,
            string? motivo,
            CancellationToken ct);

        // Atribuir responsável
        Task<Chamado> AtribuirResponsavelAsync(
            long chamadoId,
            int tenantId,
            int? responsavelUsuarioId,
            int usuarioId,
            string? motivo,
            CancellationToken ct);

        // Listar histórico de status
        Task<IReadOnlyList<(ChamadoStatusHistorico Historico, string? AlteradoPorNome)>> ListarHistoricoStatusAsync(
            long chamadoId,
            int tenantId,
            CancellationToken ct);

        // Deletar chamado (soft delete)
        Task DeletarAsync(
            long chamadoId,
            int tenantId,
            int usuarioId,
            string motivo,
            CancellationToken ct);

        // Atualizar datas de resolução/fechamento
        Task<Chamado> AtualizarDatasStatusAsync(
            long chamadoId,
            int tenantId,
            DateTime? dataResolvido,
            DateTime? dataFechado,
            CancellationToken ct);
    }
}
