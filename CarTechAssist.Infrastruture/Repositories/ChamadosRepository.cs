using System.Data;
using Dapper;
using CarTechAssist.Domain.Entities;
using CarTechAssist.Domain.Interfaces;

namespace CarTechAssist.Infrastruture.Repositories
{
    public class ChamadosRepository : IChamadosRepository
    {
        private readonly IDbConnection _db;

        public ChamadosRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<Chamado?> ObterAsync(long chamadoId, CancellationToken ct)
        {
            const string sql = @"
                SELECT * FROM core.vw_chamados 
                WHERE ChamadoId = @chamadoId AND Excluido = 0";

            return await _db.QueryFirstOrDefaultAsync<Chamado>(
                new CommandDefinition(sql, new { chamadoId }, cancellationToken: ct));
        }

        public async Task<(IReadOnlyList<Chamado> Items, int Total)> ListaAsync(
            int tenantId,
            byte? statusId,
            int? responsaveUsuarioId,
            int? solicitanteUsuarioId,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            var offset = (page - 1) * pageSize;

            var whereConditions = new List<string> { "TenantId = @tenantId", "Excluido = 0" };
            var parameters = new DynamicParameters();
            parameters.Add("tenantId", tenantId);
            parameters.Add("offset", offset);
            parameters.Add("pageSize", pageSize);

            if (statusId.HasValue)
            {
                whereConditions.Add("StatusId = @statusId");
                parameters.Add("statusId", statusId.Value);
            }

            if (responsaveUsuarioId.HasValue)
            {
                whereConditions.Add("ResponsavelUsuarioId = @responsavelUsuarioId");
                parameters.Add("responsavelUsuarioId", responsaveUsuarioId.Value);
            }

            if (solicitanteUsuarioId.HasValue)
            {
                whereConditions.Add("SolicitanteUsuarioId = @solicitanteUsuarioId");
                parameters.Add("solicitanteUsuarioId", solicitanteUsuarioId.Value);
            }

            var whereClause = string.Join(" AND ", whereConditions);

            var sql = $@"
                SELECT * FROM core.vw_chamados 
                WHERE {whereClause}
                ORDER BY DataCriacao DESC
                OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;

                SELECT COUNT(*) FROM core.vw_chamados 
                WHERE {whereClause}";

            using var multi = await _db.QueryMultipleAsync(
                new CommandDefinition(sql, parameters, cancellationToken: ct));

            var items = (await multi.ReadAsync<Chamado>()).ToList();
            var total = await multi.ReadSingleAsync<int>();

            return (items, total);
        }

        public async Task<Chamado> CriarAsync(
            int tenantId,
            string titulo,
            string? descricao,
            int? categoriaId,
            byte prioridadeId,
            byte canalId,
            int solicitanteUsuarioId,
            int? responsavelUsuarioId,
            CancellationToken ct)
        {
            const string sql = @"
                EXEC core.usp_Chamado_Criar
                    @TenantId = @tenantId,
                    @Titulo = @titulo,
                    @Descricao = @descricao,
                    @CategoriaId = @categoriaId,
                    @PrioridadeId = @prioridadeId,
                    @CanalId = @canalId,
                    @SolicitanteUsuarioId = @solicitanteUsuarioId,
                    @ResponsavelUsuarioId = @responsavelUsuarioId";

            var parameters = new DynamicParameters();
            parameters.Add("tenantId", tenantId);
            parameters.Add("titulo", titulo);
            parameters.Add("descricao", descricao == null ? (object)DBNull.Value : descricao);
            parameters.Add("categoriaId", categoriaId ?? (object)DBNull.Value);
            parameters.Add("prioridadeId", prioridadeId);
            parameters.Add("canalId", canalId);
            parameters.Add("solicitanteUsuarioId", solicitanteUsuarioId);
            parameters.Add("responsavelUsuarioId", responsavelUsuarioId ?? (object)DBNull.Value);

            return await _db.QueryFirstAsync<Chamado>(
                new CommandDefinition(sql, parameters, cancellationToken: ct));
        }

        public async Task<Chamado> AdicionarInteracaoIaAsync(
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
            CancellationToken ct)
        {
            const string sql = @"
                EXEC core.usp_Chamado_IA_AdicionarInteracao
                    @ChamadoId = @chamadoId,
                    @TenantId = @tenantId,
                    @Modelo = @modelo,
                    @Mensagem = @mensagem,
                    @Confianca = @confianca,
                    @ResumoRaciocinio = @resumoRaciocinio,
                    @Provedor = @provedor,
                    @InputTokens = @inputTokens,
                    @OutputTokens = @outputTokens,
                    @CustoUsd = @custoUsd";

            var parameters = new DynamicParameters();
            parameters.Add("chamadoId", chamadoId);
            parameters.Add("tenantId", tenantId);
            parameters.Add("modelo", modelo);
            parameters.Add("mensagem", mensagem);
            parameters.Add("confianca", confianca ?? (object)DBNull.Value);
            parameters.Add("resumoRaciocinio", resumoRaciocinio ?? (object)DBNull.Value);
            parameters.Add("provedor", provedor);
            parameters.Add("inputTokens", inputTokens ?? (object)DBNull.Value);
            parameters.Add("outputTokens", outputTokens ?? (object)DBNull.Value);
            parameters.Add("custoUsd", custoUsd ?? (object)DBNull.Value);

            return await _db.QueryFirstAsync<Chamado>(
                new CommandDefinition(sql, parameters, cancellationToken: ct));
        }

        public async Task<Chamado> AdicionarInteracaoAsync(
            long chamadoId,
            int tenantId,
            int usuarioId,
            string mensagem,
            CancellationToken ct)
        {
            const string sql = @"
                EXEC core.usp_Chamado_AdicionarInteracao
                    @ChamadoId = @chamadoId,
                    @TenantId = @tenantId,
                    @UsuarioId = @usuarioId,
                    @Mensagem = @mensagem";

            var parameters = new DynamicParameters();
            parameters.Add("chamadoId", chamadoId);
            parameters.Add("tenantId", tenantId);
            parameters.Add("usuarioId", usuarioId);
            parameters.Add("mensagem", mensagem);

            return await _db.QueryFirstAsync<Chamado>(
                new CommandDefinition(sql, parameters, cancellationToken: ct));
        }

        public async Task<Chamado> AlterarStatusAsync(
            long chamadoId,
            int tenantId,
            byte novoStatus,
            int usuarioId,
            CancellationToken ct)
        {
            const string sql = @"
                EXEC core.usp_Chamado_AlterarStatus
                    @ChamadoId = @chamadoId,
                    @TenantId = @tenantId,
                    @NovoStatus = @novoStatus,
                    @UsuarioId = @usuarioId";

            var parameters = new DynamicParameters();
            parameters.Add("chamadoId", chamadoId);
            parameters.Add("tenantId", tenantId);
            parameters.Add("novoStatus", novoStatus);
            parameters.Add("usuarioId", usuarioId);

            return await _db.QueryFirstAsync<Chamado>(
                new CommandDefinition(sql, parameters, cancellationToken: ct));
        }

        public async Task AdicionarAnexoAsync(
            long chamadoId,
            int tenantId,
            string nomeArquivo,
            string contentType,
            byte[] bytes,
            int usuarioId,
            CancellationToken ct)
        {
            const string sql = @"
                EXEC core.usp_Chamado_AdicionarAnexo
                    @ChamadoId = @chamadoId,
                    @TenantId = @tenantId,
                    @NomeArquivo = @nomeArquivo,
                    @ContentType = @contentType,
                    @Bytes = @bytes,
                    @UsuarioId = @usuarioId";

            var parameters = new DynamicParameters();
            parameters.Add("chamadoId", chamadoId);
            parameters.Add("tenantId", tenantId);
            parameters.Add("nomeArquivo", nomeArquivo);
            parameters.Add("contentType", contentType);
            parameters.Add("bytes", bytes);
            parameters.Add("usuarioId", usuarioId);

            await _db.ExecuteAsync(
                new CommandDefinition(sql, parameters, cancellationToken: ct));
        }

        public async Task<Chamado> AdicionarFeedbackAsync(
            long chamadoId,
            int tenantId,
            int? usuarioId,
            byte score,
            string? comentario,
            CancellationToken ct)
        {
            const string sql = @"
                -- Inserir feedback na tabela ia.IAFeedback
                INSERT INTO ia.IAFeedback (TenantId, ChamadoId, DadoPorUsuarioId, Score, Comentario)
                VALUES (@tenantId, @chamadoId, @usuarioId, @score, @comentario);
                
                -- Atualizar o campo IA_FeedbackScore no chamado
                UPDATE core.Chamado 
                SET IA_FeedbackScore = @score,
                    DataAtualizacao = sysutcdatetime()
                WHERE ChamadoId = @chamadoId AND TenantId = @tenantId AND Excluido = 0;
                
                -- Retornar o chamado atualizado
                SELECT * FROM core.vw_chamados WHERE ChamadoId = @chamadoId";

            var parameters = new DynamicParameters();
            parameters.Add("chamadoId", chamadoId);
            parameters.Add("tenantId", tenantId);
            parameters.Add("usuarioId", usuarioId ?? (object)DBNull.Value);
            parameters.Add("score", score);
            parameters.Add("comentario", comentario ?? (object)DBNull.Value);

            return await _db.QueryFirstAsync<Chamado>(
                new CommandDefinition(sql, parameters, cancellationToken: ct));
        }

        public async Task<IReadOnlyList<(ChamadoInteracao Interacao, string? AutorNome)>> ListarInteracoesComAutorAsync(
            long chamadoId,
            int tenantId,
            CancellationToken ct)
        {
            const string sql = @"
                SELECT 
                    i.InteracaoId, i.ChamadoId, i.TenantId, i.AutorUsuarioId, i.AutorTipoUsuarioId,
                    i.CanalId, i.Mensagem, i.Interna, i.IA_Gerada, i.IA_Modelo, i.IA_Confianca,
                    i.IA_ResumoRaciocinio, i.DataCriacao, i.Excluido, i.RowVer,
                    AutorNome = u.NomeCompleto
                FROM core.ChamadoInteracao i
                LEFT JOIN core.Usuario u ON i.AutorUsuarioId = u.UsuarioId AND u.Excluido = 0
                WHERE i.ChamadoId = @chamadoId 
                    AND i.TenantId = @tenantId 
                    AND i.Excluido = 0
                ORDER BY i.DataCriacao ASC";

            // Usar Dapper multi-mapping para melhor performance - evita N+1 query
            // splitOn: "AutorNome" indica onde começa o segundo objeto (após RowVer)
            var results = await _db.QueryAsync<ChamadoInteracao, string, (ChamadoInteracao, string?)>(
                new CommandDefinition(sql, new { chamadoId, tenantId }, cancellationToken: ct),
                (interacao, autorNome) => (interacao, autorNome ?? null),
                splitOn: "AutorNome");

            return results.ToList();
        }

        public async Task<IReadOnlyList<ChamadoInteracao>> ListarInteracoesAsync(
            long chamadoId,
            int tenantId,
            CancellationToken ct)
        {
            // Manter método original para compatibilidade, mas usar o novo método otimizado via service
            const string sql = @"
                SELECT i.*
                FROM core.ChamadoInteracao i
                WHERE i.ChamadoId = @chamadoId 
                    AND i.TenantId = @tenantId 
                    AND i.Excluido = 0
                ORDER BY i.DataCriacao ASC";

            var interacoes = await _db.QueryAsync<ChamadoInteracao>(
                new CommandDefinition(sql, new { chamadoId, tenantId }, cancellationToken: ct));

            return interacoes.ToList();
        }

        public async Task<ChamadoAnexo?> ObterAnexoAsync(
            long anexoId,
            int tenantId,
            CancellationToken ct)
        {
            const string sql = @"
                SELECT * FROM core.ChamadoAnexo 
                WHERE AnexoId = @anexoId 
                    AND TenantId = @tenantId 
                    AND Excluido = 0";

            return await _db.QueryFirstOrDefaultAsync<ChamadoAnexo>(
                new CommandDefinition(sql, new { anexoId, tenantId }, cancellationToken: ct));
        }

        public async Task<IReadOnlyList<ChamadoAnexo>> ListarAnexosAsync(
            long chamadoId,
            int tenantId,
            CancellationToken ct)
        {
            const string sql = @"
                SELECT AnexoId, ChamadoId, InteracaoId, TenantId, NomeArquivo, 
                       ContentType, TamanhoBytes, UrlExterna, DataCriacao, Excluido, RowVer
                FROM core.ChamadoAnexo 
                WHERE ChamadoId = @chamadoId 
                    AND TenantId = @tenantId 
                    AND Excluido = 0
                ORDER BY DataCriacao DESC";

            var anexos = await _db.QueryAsync<ChamadoAnexo>(
                new CommandDefinition(sql, new { chamadoId, tenantId }, cancellationToken: ct));

            return anexos.ToList();
        }

        public async Task<Chamado> AtualizarAsync(
            long chamadoId,
            int tenantId,
            string? titulo,
            string? descricao,
            int? categoriaId,
            byte? prioridadeId,
            int usuarioId,
            string? motivo,
            CancellationToken ct)
        {
            const string sql = @"
                UPDATE core.Chamado 
                SET 
                    Titulo = ISNULL(@titulo, Titulo),
                    Descricao = ISNULL(@descricao, Descricao),
                    CategoriaId = ISNULL(@categoriaId, CategoriaId),
                    PrioridadeId = ISNULL(@prioridadeId, PrioridadeId),
                    DataAtualizacao = GETUTCDATE()
                WHERE ChamadoId = @chamadoId AND TenantId = @tenantId AND Excluido = 0;

                SELECT * FROM core.vw_chamados WHERE ChamadoId = @chamadoId";

            var parameters = new DynamicParameters();
            parameters.Add("chamadoId", chamadoId);
            parameters.Add("tenantId", tenantId);
            parameters.Add("titulo", titulo ?? (object)DBNull.Value);
            parameters.Add("descricao", descricao ?? (object)DBNull.Value);
            parameters.Add("categoriaId", categoriaId ?? (object)DBNull.Value);
            parameters.Add("prioridadeId", prioridadeId ?? (object)DBNull.Value);

            var chamado = await _db.QueryFirstOrDefaultAsync<Chamado>(
                new CommandDefinition(sql, parameters, cancellationToken: ct));

            if (chamado == null)
                throw new InvalidOperationException("Chamado não encontrado após atualização.");

            return chamado;
        }

        public async Task<Chamado> AtribuirResponsavelAsync(
            long chamadoId,
            int tenantId,
            int? responsavelUsuarioId,
            int usuarioId,
            string? motivo,
            CancellationToken ct)
        {
            // Calcular SLA baseado na prioridade
            const string sql = @"
                DECLARE @prioridadeId TINYINT;
                SELECT @prioridadeId = PrioridadeId FROM core.Chamado WHERE ChamadoId = @chamadoId AND TenantId = @tenantId;

                DECLARE @horasSLA INT;
                SET @horasSLA = CASE @prioridadeId
                    WHEN 4 THEN 4  -- Urgente
                    WHEN 3 THEN 8  -- Alta
                    WHEN 2 THEN 24 -- Média
                    WHEN 1 THEN 72 -- Baixa
                    ELSE 72
                END;

                UPDATE core.Chamado 
                SET 
                    ResponsavelUsuarioId = @responsavelUsuarioId,
                    SLA_EstimadoFim = CASE 
                        WHEN @responsavelUsuarioId IS NOT NULL THEN DATEADD(HOUR, @horasSLA, GETUTCDATE())
                        ELSE NULL
                    END,
                    DataAtualizacao = GETUTCDATE()
                WHERE ChamadoId = @chamadoId AND TenantId = @tenantId AND Excluido = 0;

                SELECT * FROM core.vw_chamados WHERE ChamadoId = @chamadoId";

            var parameters = new DynamicParameters();
            parameters.Add("chamadoId", chamadoId);
            parameters.Add("tenantId", tenantId);
            parameters.Add("responsavelUsuarioId", responsavelUsuarioId ?? (object)DBNull.Value);

            var chamado = await _db.QueryFirstOrDefaultAsync<Chamado>(
                new CommandDefinition(sql, parameters, cancellationToken: ct));

            if (chamado == null)
                throw new InvalidOperationException("Chamado não encontrado após atribuição.");

            return chamado;
        }

        public async Task<IReadOnlyList<(ChamadoStatusHistorico Historico, string? AlteradoPorNome)>> ListarHistoricoStatusAsync(
            long chamadoId,
            int tenantId,
            CancellationToken ct)
        {
            const string sql = @"
                SELECT 
                    h.HistoricoId, h.ChamadoId, h.TenantId, h.StatusAntigoId, h.StatusNovoId,
                    h.AlteradoPorUsuarioId, h.Motivo, h.DataAlteracao,
                    u.NomeCompleto as AlteradoPorNome
                FROM log.ChamadoStatusHistorico h
                LEFT JOIN core.Usuario u ON h.AlteradoPorUsuarioId = u.UsuarioId AND u.Excluido = 0
                WHERE h.ChamadoId = @chamadoId AND h.TenantId = @tenantId
                ORDER BY h.DataAlteracao DESC";

            var results = await _db.QueryAsync<ChamadoStatusHistorico, string, (ChamadoStatusHistorico, string?)>(
                new CommandDefinition(sql, new { chamadoId, tenantId }, cancellationToken: ct),
                (historico, nome) => (historico, nome ?? null),
                splitOn: "AlteradoPorNome");

            return results.ToList();
        }

        public async Task DeletarAsync(
            long chamadoId,
            int tenantId,
            int usuarioId,
            string motivo,
            CancellationToken ct)
        {
            const string sql = @"
                UPDATE core.Chamado 
                SET 
                    Excluido = 1,
                    DataAtualizacao = GETUTCDATE()
                WHERE ChamadoId = @chamadoId AND TenantId = @tenantId AND Excluido = 0;

                -- Registrar motivo da exclusão (pode ser em uma tabela de auditoria)
                -- Por enquanto, apenas soft delete";

            var parameters = new DynamicParameters();
            parameters.Add("chamadoId", chamadoId);
            parameters.Add("tenantId", tenantId);
            parameters.Add("motivo", motivo);

            var rowsAffected = await _db.ExecuteAsync(
                new CommandDefinition(sql, parameters, cancellationToken: ct));

            if (rowsAffected == 0)
                throw new InvalidOperationException("Chamado não encontrado ou já foi deletado.");
        }

        public async Task<Chamado> AtualizarDatasStatusAsync(
            long chamadoId,
            int tenantId,
            DateTime? dataResolvido,
            DateTime? dataFechado,
            CancellationToken ct)
        {
            const string sql = @"
                UPDATE core.Chamado 
                SET 
                    DataResolvido = ISNULL(@dataResolvido, DataResolvido),
                    DataFechado = ISNULL(@dataFechado, DataFechado),
                    DataAtualizacao = GETUTCDATE()
                WHERE ChamadoId = @chamadoId AND TenantId = @tenantId AND Excluido = 0;

                SELECT * FROM core.vw_chamados WHERE ChamadoId = @chamadoId";

            var parameters = new DynamicParameters();
            parameters.Add("chamadoId", chamadoId);
            parameters.Add("tenantId", tenantId);
            parameters.Add("dataResolvido", dataResolvido ?? (object)DBNull.Value);
            parameters.Add("dataFechado", dataFechado ?? (object)DBNull.Value);

            var chamado = await _db.QueryFirstOrDefaultAsync<Chamado>(
                new CommandDefinition(sql, parameters, cancellationToken: ct));

            if (chamado == null)
                throw new InvalidOperationException("Chamado não encontrado após atualização de datas.");

            return chamado;
        }
    }
}
