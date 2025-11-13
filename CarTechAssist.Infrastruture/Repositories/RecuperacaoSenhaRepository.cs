using CarTechAssist.Domain.Interfaces;
using Dapper;
using System.Data;

namespace CarTechAssist.Infrastruture.Repositories
{
    public class RecuperacaoSenhaRepository : IRecuperacaoSenhaRepository
    {
        private readonly IDbConnection _db;

        public RecuperacaoSenhaRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<Domain.Entities.RecuperacaoSenha?> ObterPorCodigoAsync(string codigo, CancellationToken ct)
        {
            // Garantir que a conexão está aberta
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

            var sql = @"
                SELECT TOP 1
                    RecuperacaoSenhaId,
                    TenantId,
                    UsuarioId,
                    Codigo,
                    Email,
                    DataExpiracao,
                    Usado,
                    DataCriacao,
                    DataUso
                FROM core.RecuperacaoSenha
                WHERE Codigo = @Codigo AND Usado = 0 AND DataExpiracao > GETDATE()";

            return await _db.QueryFirstOrDefaultAsync<Domain.Entities.RecuperacaoSenha>(
                new CommandDefinition(sql, new { Codigo = codigo }, cancellationToken: ct));
        }

        public async Task<Domain.Entities.RecuperacaoSenha?> ObterPorUsuarioAsync(int tenantId, int usuarioId, CancellationToken ct)
        {
            // Garantir que a conexão está aberta
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

            var sql = @"
                SELECT TOP 1
                    RecuperacaoSenhaId,
                    TenantId,
                    UsuarioId,
                    Codigo,
                    Email,
                    DataExpiracao,
                    Usado,
                    DataCriacao,
                    DataUso
                FROM core.RecuperacaoSenha
                WHERE TenantId = @TenantId 
                    AND UsuarioId = @UsuarioId 
                    AND Usado = 0 
                    AND DataExpiracao > GETDATE()
                ORDER BY DataCriacao DESC";

            return await _db.QueryFirstOrDefaultAsync<Domain.Entities.RecuperacaoSenha>(
                new CommandDefinition(sql, new { TenantId = tenantId, UsuarioId = usuarioId }, cancellationToken: ct));
        }

        public async Task<long> CriarAsync(Domain.Entities.RecuperacaoSenha recuperacao, CancellationToken ct)
        {
            // Garantir que a conexão está aberta
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

            var sql = @"
                INSERT INTO core.RecuperacaoSenha 
                    (TenantId, UsuarioId, Codigo, Email, DataExpiracao, Usado, DataCriacao)
                OUTPUT INSERTED.RecuperacaoSenhaId
                VALUES 
                    (@TenantId, @UsuarioId, @Codigo, @Email, @DataExpiracao, 0, GETDATE())";

            var id = await _db.ExecuteScalarAsync<long>(
                new CommandDefinition(sql, recuperacao, cancellationToken: ct));
            return id;
        }

        public async Task MarcarComoUsadoAsync(long recuperacaoSenhaId, CancellationToken ct)
        {
            // Garantir que a conexão está aberta
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

            var sql = @"
                UPDATE core.RecuperacaoSenha
                SET Usado = 1, DataUso = GETDATE()
                WHERE RecuperacaoSenhaId = @RecuperacaoSenhaId";

            await _db.ExecuteAsync(
                new CommandDefinition(sql, new { RecuperacaoSenhaId = recuperacaoSenhaId }, cancellationToken: ct));
        }

        public async Task LimparExpiradasAsync(int tenantId, CancellationToken ct)
        {
            // Garantir que a conexão está aberta
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

            var sql = @"
                DELETE FROM core.RecuperacaoSenha
                WHERE TenantId = @TenantId 
                    AND (DataExpiracao < GETDATE() OR Usado = 1)";

            await _db.ExecuteAsync(
                new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
        }
    }
}

