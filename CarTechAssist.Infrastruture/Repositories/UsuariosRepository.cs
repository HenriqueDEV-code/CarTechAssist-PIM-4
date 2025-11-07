using System.Data;
using Dapper;
using CarTechAssist.Domain.Entities;
using CarTechAssist.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CarTechAssist.Infrastruture.Repositories
{
    public class UsuariosRepository : IUsuariosRepository
    {
        private readonly IDbConnection _db;
        private readonly ILogger<UsuariosRepository> _logger;

        public UsuariosRepository(IDbConnection db, ILogger<UsuariosRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<Usuario?> ObterPorLoginAsync(int tenantId, string login, CancellationToken ct)
        {
            const string sql = @"
                SELECT * FROM core.Usuario 
                WHERE TenantId = @tenantId AND Login = @login AND Excluido = 0";

            return await _db.QueryFirstOrDefaultAsync<Usuario>(
                new CommandDefinition(sql, new { tenantId, login }, cancellationToken: ct));
        }

        public async Task<Usuario?> ObterPorIdAsync(int usuarioId, CancellationToken ct)
        {
            const string sql = @"
                SELECT * FROM core.Usuario 
                WHERE UsuarioId = @usuarioId AND Excluido = 0";

            return await _db.QueryFirstOrDefaultAsync<Usuario>(
                new CommandDefinition(sql, new { usuarioId }, cancellationToken: ct));
        }

        public async Task<(IReadOnlyList<Usuario> Items, int Total)> ListarAsync(
            int tenantId,
            byte? tipoUsuarioId,
            bool? ativo,
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

            if (tipoUsuarioId.HasValue)
            {
                whereConditions.Add("TipoUsuarioId = @tipoUsuarioId");
                parameters.Add("tipoUsuarioId", tipoUsuarioId.Value);
            }

            if (ativo.HasValue)
            {
                whereConditions.Add("Ativo = @ativo");
                parameters.Add("ativo", ativo.Value);
            }

            var whereClause = string.Join(" AND ", whereConditions);

            var sql = $@"
                SELECT * FROM core.Usuario 
                WHERE {whereClause}
                ORDER BY NomeCompleto
                OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;

                SELECT COUNT(*) FROM core.Usuario 
                WHERE {whereClause}";

            using var multi = await _db.QueryMultipleAsync(
                new CommandDefinition(sql, parameters, cancellationToken: ct));

            var items = (await multi.ReadAsync<Usuario>()).ToList();
            var total = await multi.ReadSingleAsync<int>();

            return (items, total);
        }

        public async Task<Usuario> CriarAsync(Usuario usuario, CancellationToken ct)
        {
            _logger.LogInformation("🔵 REPOSITORY: Iniciando criação de usuário. Login: {Login}, TenantId: {TenantId}, TipoUsuarioId: {TipoUsuarioId}",
                usuario.Login, usuario.TenantId, usuario.TipoUsuarioId);

            try
            {
                // Verificar se a conexão está aberta
                if (_db.State != ConnectionState.Open)
                {
                    _logger.LogWarning("⚠️ Conexão não está aberta. Estado: {State}. Abrindo conexão...", _db.State);
                    _db.Open();
                }

                _logger.LogInformation("✅ Conexão aberta. Estado: {State}", _db.State);

                const string sql = @"
                    INSERT INTO core.Usuario 
                        (TenantId, TipoUsuarioId, Login, NomeCompleto, Email, Telefone, 
                         HashSenha, SaltSenha, PrecisaTrocarSenha, Ativo, DataCriacao, Excluido)
                    VALUES 
                        (@TenantId, @TipoUsuarioId, @Login, @NomeCompleto, @Email, @Telefone,
                         @HashSenha, @SaltSenha, @PrecisaTrocarSenha, @Ativo, @DataCriacao, @Excluido);
                    SELECT CAST(SCOPE_IDENTITY() as int);";

                _logger.LogInformation("📝 SQL preparado. Executando INSERT...");
                _logger.LogDebug("Parâmetros: TenantId={TenantId}, Login={Login}, NomeCompleto={NomeCompleto}, " +
                    "Email={Email}, Telefone={Telefone}, HashSenhaLength={HashSenhaLength}, SaltSenhaLength={SaltSenhaLength}, " +
                    "PrecisaTrocarSenha={PrecisaTrocarSenha}, Ativo={Ativo}, DataCriacao={DataCriacao}, Excluido={Excluido}",
                    usuario.TenantId, usuario.Login, usuario.NomeCompleto, usuario.Email, usuario.Telefone,
                    usuario.HashSenha?.Length ?? 0, usuario.SaltSenha?.Length ?? 0,
                    usuario.PrecisaTrocarSenha, usuario.Ativo, usuario.DataCriacao, usuario.Excluido);

                var usuarioId = await _db.QuerySingleAsync<int>(
                    new CommandDefinition(sql, usuario, cancellationToken: ct));

                _logger.LogInformation("✅ INSERT executado com sucesso! UsuarioId retornado: {UsuarioId}", usuarioId);

                usuario.UsuarioId = usuarioId;
                
                _logger.LogInformation("🎉 Usuário criado com sucesso no banco de dados! UsuarioId: {UsuarioId}, Login: {Login}",
                    usuario.UsuarioId, usuario.Login);

                return usuario;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ERRO ao criar usuário no repositório. Tipo: {Type}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        public async Task<Usuario> AtualizarAsync(Usuario usuario, CancellationToken ct)
        {
            const string sql = @"
                UPDATE core.Usuario 
                SET NomeCompleto = @NomeCompleto,
                    Email = @Email,
                    Telefone = @Telefone,
                    TipoUsuarioId = @TipoUsuarioId,
                    DataAtualizacao = @DataAtualizacao
                WHERE UsuarioId = @UsuarioId AND Excluido = 0";

            await _db.ExecuteAsync(
                new CommandDefinition(sql, usuario, cancellationToken: ct));

            return usuario;
        }

        public async Task AlterarAtivacaoAsync(int usuarioId, bool ativo, CancellationToken ct)
        {
            const string sql = @"
                UPDATE core.Usuario 
                SET Ativo = @ativo
                WHERE UsuarioId = @usuarioId AND Excluido = 0";

            await _db.ExecuteAsync(
                new CommandDefinition(sql, new { usuarioId, ativo }, cancellationToken: ct));
        }

        public async Task AtualizarSenhaAsync(int usuarioId, byte[] hash, byte[] salt, CancellationToken ct)
        {
            const string sql = @"
                UPDATE core.Usuario 
                SET HashSenha = @hash,
                    SaltSenha = @salt,
                    PrecisaTrocarSenha = 0
                WHERE UsuarioId = @usuarioId AND Excluido = 0";

            await _db.ExecuteAsync(
                new CommandDefinition(sql, new { usuarioId, hash, salt }, cancellationToken: ct));
        }

        public async Task<bool> ExisteLoginAsync(int tenantId, string login, CancellationToken ct)
        {
            const string sql = @"
                SELECT COUNT(*) FROM core.Usuario 
                WHERE TenantId = @tenantId AND Login = @login AND Excluido = 0";

            var count = await _db.QuerySingleAsync<int>(
                new CommandDefinition(sql, new { tenantId, login }, cancellationToken: ct));

            return count > 0;
        }
    }
}
