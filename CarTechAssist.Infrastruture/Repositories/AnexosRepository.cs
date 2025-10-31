using System.Data;
using Dapper;
using CarTechAssist.Domain.Entities;
using CarTechAssist.Domain.Interfaces;

namespace CarTechAssist.Infrastruture.Repositories
{
    public class AnexosRepository : IAnexosReposity
    {
        private readonly IDbConnection _db;

        public AnexosRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<long> AdicionarAsync(ChamadoAnexo anexo, CancellationToken ct)
        {
            const string sql = @"
                INSERT INTO core.ChamadoAnexo 
                    (ChamadoId, InteracaoId, TenantId, NomeArquivo, ContentType, 
                     TamanhoBytes, Conteudo, UrlExterna, HashConteudo, DataCriacao, Excluido)
                VALUES 
                    (@ChamadoId, @InteracaoId, @TenantId, @NomeArquivo, @ContentType,
                     @TamanhoBytes, @Conteudo, @UrlExterna, @HashConteudo, @DataCriacao, @Excluido);
                SELECT CAST(SCOPE_IDENTITY() as bigint);";

            var anexoId = await _db.QuerySingleAsync<long>(
                new CommandDefinition(sql, anexo, cancellationToken: ct));

            return anexoId;
        }

        public async Task<IReadOnlyList<ChamadoAnexo>> ListarPorChamadoAsync(int chamadoId, CancellationToken ct)
        {
            const string sql = @"
                SELECT AnexoId, ChamadoId, InteracaoId, TenantId, NomeArquivo, 
                       ContentType, TamanhoBytes, UrlExterna, DataCriacao, Excluido, RowVer
                FROM core.ChamadoAnexo 
                WHERE ChamadoId = @chamadoId AND Excluido = 0
                ORDER BY DataCriacao DESC";

            var anexos = await _db.QueryAsync<ChamadoAnexo>(
                new CommandDefinition(sql, new { chamadoId }, cancellationToken: ct));

            return anexos.ToList();
        }
    }
}

