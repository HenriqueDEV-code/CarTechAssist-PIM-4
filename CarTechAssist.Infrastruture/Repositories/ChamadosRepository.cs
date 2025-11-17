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
            // Garantir que a conexão está aberta
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

            const string sql = @"
                SELECT * FROM core.vw_chamados 
                WHERE ChamadoId = @chamadoId";

            return await _db.QueryFirstOrDefaultAsync<Chamado>(
                new CommandDefinition(sql, new { chamadoId }, cancellationToken: ct));
        }

        public async Task<(IReadOnlyList<Chamado> Items, int Total)> ListaAsync(
            int tenantId,
            byte? statusId,
            int? responsavelUsuarioId,
            int? solicitanteUsuarioId,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            // Garantir que a conexão está aberta
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

            var offset = (page - 1) * pageSize;

            var parameters = new DynamicParameters();
            parameters.Add("tenantId", tenantId);
            parameters.Add("offset", offset);
            parameters.Add("pageSize", pageSize);

            parameters.Add("statusId", statusId);

            parameters.Add("responsavelUsuarioId", responsavelUsuarioId);
            parameters.Add("solicitanteUsuarioId", solicitanteUsuarioId);


            const string sql = @"
                SELECT * FROM core.vw_chamados 
                WHERE TenantId = @tenantId 
                    AND (@statusId IS NULL OR StatusId = @statusId)
                    AND (@responsavelUsuarioId IS NULL OR ResponsavelUsuarioId = @responsavelUsuarioId)
                    AND (@solicitanteUsuarioId IS NULL OR SolicitanteUsuarioId = @solicitanteUsuarioId)
                ORDER BY DataCriacao DESC
                OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;

                SELECT COUNT(*) FROM core.vw_chamados 
                WHERE TenantId = @tenantId 
                    AND (@statusId IS NULL OR StatusId = @statusId)
                    AND (@responsavelUsuarioId IS NULL OR ResponsavelUsuarioId = @responsavelUsuarioId)
                    AND (@solicitanteUsuarioId IS NULL OR SolicitanteUsuarioId = @solicitanteUsuarioId)";

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
            DateTime? slaEstimadoFim,
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
                    @ResponsavelUsuarioId = @responsavelUsuarioId,
                    @SLA_EstimadoFim = @slaEstimadoFim";

            var parameters = new DynamicParameters();
            parameters.Add("tenantId", tenantId);
            parameters.Add("titulo", titulo);

            parameters.Add("descricao", descricao);
            parameters.Add("categoriaId", categoriaId);
            parameters.Add("prioridadeId", prioridadeId);
            parameters.Add("canalId", canalId);
            parameters.Add("solicitanteUsuarioId", solicitanteUsuarioId);

            parameters.Add("responsavelUsuarioId", responsavelUsuarioId);

            parameters.Add("slaEstimadoFim", slaEstimadoFim);

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

            parameters.Add("confianca", confianca);
            parameters.Add("resumoRaciocinio", resumoRaciocinio);
            parameters.Add("provedor", provedor);
            parameters.Add("inputTokens", inputTokens);
            parameters.Add("outputTokens", outputTokens);
            parameters.Add("custoUsd", custoUsd);

            return await _db.QueryFirstAsync<Chamado>(
                new CommandDefinition(sql, parameters, cancellationToken: ct));
        }

        public async Task<ChamadoInteracao> AdicionarInteracaoAsync(
            long chamadoId,
            int tenantId,
            int usuarioId,
            string mensagem,
            CancellationToken ct)
        {
            // Garantir que a conexão está aberta
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

            // Primeiro, verificar se o chamado existe e pertence ao tenant
            const string checkSql = @"
                SELECT ChamadoId, TenantId, CanalId, SolicitanteUsuarioId
                FROM core.Chamado 
                WHERE ChamadoId = @chamadoId AND TenantId = @tenantId AND Excluido = 0";

            var chamadoInfo = await _db.QueryFirstOrDefaultAsync<dynamic>(
                new CommandDefinition(checkSql, new { chamadoId, tenantId }, cancellationToken: ct));

            if (chamadoInfo == null)
            {
                throw new InvalidOperationException($"Chamado {chamadoId} não encontrado ou não pertence ao tenant {tenantId}.");
            }

            // Obter informações do usuário para determinar o tipo
            const string usuarioSql = @"
                SELECT UsuarioId, TipoUsuarioId
                FROM core.Usuario
                WHERE UsuarioId = @usuarioId AND TenantId = @tenantId AND Excluido = 0";

            var usuarioInfo = await _db.QueryFirstOrDefaultAsync<dynamic>(
                new CommandDefinition(usuarioSql, new { usuarioId, tenantId }, cancellationToken: ct));

            if (usuarioInfo == null)
            {
                throw new InvalidOperationException($"Usuário {usuarioId} não encontrado ou não pertence ao tenant {tenantId}.");
            }

            var autorTipoUsuarioId = (byte)usuarioInfo.TipoUsuarioId;
            var canalId = (byte)chamadoInfo.CanalId;

            // Inserir a interação
            const string insertSql = @"
                INSERT INTO core.ChamadoInteracao 
                    (ChamadoId, TenantId, AutorUsuarioId, AutorTipoUsuarioId, CanalId, Mensagem, Interna, IA_Gerada, DataCriacao, Excluido)
                OUTPUT INSERTED.*
                VALUES 
                    (@chamadoId, @tenantId, @autorUsuarioId, @autorTipoUsuarioId, @canalId, @mensagem, 0, 0, GETUTCDATE(), 0)";

            var parameters = new DynamicParameters();
            parameters.Add("chamadoId", chamadoId);
            parameters.Add("tenantId", tenantId);
            parameters.Add("autorUsuarioId", usuarioId);
            parameters.Add("autorTipoUsuarioId", autorTipoUsuarioId);
            parameters.Add("canalId", canalId);
            parameters.Add("mensagem", mensagem);

            var interacao = await _db.QueryFirstAsync<ChamadoInteracao>(
                new CommandDefinition(insertSql, parameters, cancellationToken: ct));

            // Atualizar DataAtualizacao do chamado
            const string updateChamadoSql = @"
                UPDATE core.Chamado 
                SET DataAtualizacao = GETUTCDATE()
                WHERE ChamadoId = @chamadoId AND TenantId = @tenantId";

            await _db.ExecuteAsync(
                new CommandDefinition(updateChamadoSql, new { chamadoId, tenantId }, cancellationToken: ct));

            return interacao;
        }

        public async Task<IReadOnlyList<ChamadoInteracao>> ListarInteracoesAsync(
            long chamadoId,
            int tenantId,
            CancellationToken ct)
        {
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

        public async Task<Chamado> AlterarStatusAsync(
            long chamadoId,
            int tenantId,
            byte novoStatus,
            int usuarioId,
            CancellationToken ct)
        {
            // Garantir que a conexão está aberta
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

            // Primeiro, verificar se o chamado existe e pertence ao tenant, e obter o status atual
            const string checkSql = @"
                SELECT ChamadoId, TenantId, StatusId 
                FROM core.Chamado 
                WHERE ChamadoId = @chamadoId AND TenantId = @tenantId AND Excluido = 0";

            var chamadoExistente = await _db.QueryFirstOrDefaultAsync<dynamic>(
                new CommandDefinition(checkSql, new { chamadoId, tenantId }, cancellationToken: ct));

            if (chamadoExistente == null)
            {
                throw new InvalidOperationException($"Chamado {chamadoId} não encontrado ou não pertence ao tenant {tenantId}.");
            }

            var statusAtual = (byte)chamadoExistente.StatusId;

            // Atualizar o status do chamado
            // Se o novo status for Resolvido (4) e o atual não for, atualizar DataResolvido
            // Se o novo status for Fechado (5) e o atual não for, atualizar DataFechado
            string updateSql;
            if (novoStatus == 4 && statusAtual != 4)
            {
                // Mudando para Resolvido - atualizar DataResolvido
                updateSql = @"
                    UPDATE core.Chamado 
                    SET StatusId = @novoStatus,
                        DataAtualizacao = GETUTCDATE(),
                        DataResolvido = GETUTCDATE()
                    WHERE ChamadoId = @chamadoId 
                        AND TenantId = @tenantId 
                        AND Excluido = 0";
            }
            else if (novoStatus == 5 && statusAtual != 5)
            {
                // Mudando para Fechado - atualizar DataFechado
                updateSql = @"
                    UPDATE core.Chamado 
                    SET StatusId = @novoStatus,
                        DataAtualizacao = GETUTCDATE(),
                        DataFechado = GETUTCDATE()
                    WHERE ChamadoId = @chamadoId 
                        AND TenantId = @tenantId 
                        AND Excluido = 0";
            }
            else
            {
                // Outros status - apenas atualizar StatusId e DataAtualizacao
                updateSql = @"
                    UPDATE core.Chamado 
                    SET StatusId = @novoStatus,
                        DataAtualizacao = GETUTCDATE()
                    WHERE ChamadoId = @chamadoId 
                        AND TenantId = @tenantId 
                        AND Excluido = 0";
            }

            var parameters = new DynamicParameters();
            parameters.Add("chamadoId", chamadoId);
            parameters.Add("tenantId", tenantId);
            parameters.Add("novoStatus", novoStatus);

            var rowsAffected = await _db.ExecuteAsync(
                new CommandDefinition(updateSql, parameters, cancellationToken: ct));

            if (rowsAffected == 0)
            {
                throw new InvalidOperationException($"Não foi possível alterar o status do chamado {chamadoId}.");
            }

            // Retornar o chamado atualizado
            const string selectSql = @"
                SELECT * FROM core.vw_chamados 
                WHERE ChamadoId = @chamadoId";

            var chamadoAtualizado = await _db.QueryFirstAsync<Chamado>(
                new CommandDefinition(selectSql, new { chamadoId }, cancellationToken: ct));

            return chamadoAtualizado;
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

        public async Task<(int Total, int Abertos, int EmAndamento, int Resolvidos, int Cancelados)> ObterEstatisticasAsync(
            int tenantId,
            int? solicitanteUsuarioId,
            CancellationToken ct)
        {
            // Garantir que a conexão está aberta
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

            var parameters = new DynamicParameters();
            parameters.Add("tenantId", tenantId);
            parameters.Add("solicitanteUsuarioId", solicitanteUsuarioId);

            const string sql = @"
                SELECT 
                    COUNT(*) as Total,
                    SUM(CASE WHEN StatusId = 1 THEN 1 ELSE 0 END) as Abertos,
                    SUM(CASE WHEN StatusId = 2 THEN 1 ELSE 0 END) as EmAndamento,
                    SUM(CASE WHEN StatusId = 4 THEN 1 ELSE 0 END) as Resolvidos,
                    SUM(CASE WHEN StatusId = 6 THEN 1 ELSE 0 END) as Cancelados
                FROM core.vw_chamados 
                WHERE TenantId = @tenantId 
                    AND (@solicitanteUsuarioId IS NULL OR SolicitanteUsuarioId = @solicitanteUsuarioId)";

            var result = await _db.QueryFirstAsync<(int Total, int Abertos, int EmAndamento, int Resolvidos, int Cancelados)>(
                new CommandDefinition(sql, parameters, cancellationToken: ct));

            return result;
        }
    }
}
