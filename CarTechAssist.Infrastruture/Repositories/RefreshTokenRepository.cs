using System.Data;
using Dapper;
using CarTechAssist.Domain.Entities;
using CarTechAssist.Domain.Interfaces;

namespace CarTechAssist.Infrastruture.Repositories
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly IDbConnection _db;

        public RefreshTokenRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<RefreshToken?> ObterPorTokenAsync(string token, CancellationToken ct)
        {
            const string sql = @"
                SELECT RefreshTokenId, UsuarioId, Token, ExpiraEm, Revogado, 
                       DataCriacao, DataRevogacao, IpAddress, UserAgent
                FROM core.RefreshToken 
                WHERE Token = @token AND Revogado = 0 AND ExpiraEm > GETDATE()";

            return await _db.QueryFirstOrDefaultAsync<RefreshToken>(
                new CommandDefinition(sql, new { token }, cancellationToken: ct));
        }

        public async Task<RefreshToken> CriarAsync(RefreshToken refreshToken, CancellationToken ct)
        {
            const string sql = @"
                INSERT INTO core.RefreshToken 
                    (UsuarioId, Token, ExpiraEm, Revogado, DataCriacao, IpAddress, UserAgent)
                VALUES 
                    (@UsuarioId, @Token, @ExpiraEm, @Revogado, @DataCriacao, @IpAddress, @UserAgent);
                SELECT CAST(SCOPE_IDENTITY() as bigint);";

            var refreshTokenId = await _db.QuerySingleAsync<long>(
                new CommandDefinition(sql, refreshToken, cancellationToken: ct));

            refreshToken.RefreshTokenId = refreshTokenId;
            return refreshToken;
        }

        public async Task RevogarAsync(long refreshTokenId, CancellationToken ct)
        {
            const string sql = @"
                UPDATE core.RefreshToken 
                SET Revogado = 1, DataRevogacao = GETDATE()
                WHERE RefreshTokenId = @refreshTokenId";

            await _db.ExecuteAsync(
                new CommandDefinition(sql, new { refreshTokenId }, cancellationToken: ct));
        }

        public async Task RevogarTodosDoUsuarioAsync(int usuarioId, CancellationToken ct)
        {
            const string sql = @"
                UPDATE core.RefreshToken 
                SET Revogado = 1, DataRevogacao = GETDATE()
                WHERE UsuarioId = @usuarioId AND Revogado = 0";

            await _db.ExecuteAsync(
                new CommandDefinition(sql, new { usuarioId }, cancellationToken: ct));
        }

        public async Task LimparExpiradosAsync(CancellationToken ct)
        {
            const string sql = @"
                DELETE FROM core.RefreshToken 
                WHERE ExpiraEm < DATEADD(day, -30, GETDATE())";

            await _db.ExecuteAsync(
                new CommandDefinition(sql, cancellationToken: ct));
        }
    }
}

