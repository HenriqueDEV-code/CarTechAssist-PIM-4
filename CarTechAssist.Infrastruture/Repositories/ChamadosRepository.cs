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

        public async Task<(int Total, int Abertos, int EmAndamento, int Resolvidos, int Cancelados)> ObterEstatisticasAsync(
            int tenantId,
            int? solicitanteUsuarioId,
            CancellationToken ct)
        {
            var whereConditions = new List<string> { "TenantId = @tenantId", "Excluido = 0" };
            var parameters = new DynamicParameters();
            parameters.Add("tenantId", tenantId);

            if (solicitanteUsuarioId.HasValue)
            {
                whereConditions.Add("SolicitanteUsuarioId = @solicitanteUsuarioId");
                parameters.Add("solicitanteUsuarioId", solicitanteUsuarioId.Value);
            }

            var whereClause = string.Join(" AND ", whereConditions);

            var sql = $@"
                SELECT 
                    COUNT(*) as Total,
                    SUM(CASE WHEN StatusId = 1 THEN 1 ELSE 0 END) as Abertos,
                    SUM(CASE WHEN StatusId = 2 THEN 1 ELSE 0 END) as EmAndamento,
                    SUM(CASE WHEN StatusId = 4 THEN 1 ELSE 0 END) as Resolvidos,
                    SUM(CASE WHEN StatusId = 6 THEN 1 ELSE 0 END) as Cancelados
                FROM core.vw_chamados 
                WHERE {whereClause}";

            var result = await _db.QueryFirstAsync<(int Total, int Abertos, int EmAndamento, int Resolvidos, int Cancelados)>(
                new CommandDefinition(sql, parameters, cancellationToken: ct));

            return result;
        }
    }
}
