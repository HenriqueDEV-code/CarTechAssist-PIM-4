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
