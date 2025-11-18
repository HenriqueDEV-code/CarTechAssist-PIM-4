using System.Data;
using Dapper;
using CarTechAssist.Domain.Entities;
using CarTechAssist.Domain.Interfaces;

namespace CarTechAssist.Infrastruture.Repositories
{
    public class UsuariosRepository : IUsuariosRepository
    {
        private readonly IDbConnection _db;

        public UsuariosRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<Usuario?> ObterPorLoginAsync(int tenantId, string login, CancellationToken ct)
        {
            // Garantir que a conexão está aberta
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

            const string sql = @"
                SELECT * FROM core.Usuario 
                WHERE TenantId = @tenantId AND Login = @login AND Excluido = 0";

            return await _db.QueryFirstOrDefaultAsync<Usuario>(
                new CommandDefinition(sql, new { tenantId, login }, cancellationToken: ct));
        }

        public async Task<Usuario?> ObterPorIdAsync(int usuarioId, CancellationToken ct)
        {
            // Garantir que a conexão está aberta
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

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
            try
            {
                // Garantir que a conexão está aberta
                if (_db.State != ConnectionState.Open)
                {
                    _db.Open();
                }

                var offset = (page - 1) * pageSize;

                // CORREÇÃO: Construir SQL de forma segura para evitar SQL Injection
                // Usar apenas valores hardcoded e seguros para construir a query
                var whereConditions = new List<string> { "TenantId = @tenantId", "Excluido = 0" };
                var parameters = new DynamicParameters();
                parameters.Add("tenantId", tenantId);
                parameters.Add("offset", offset);
                parameters.Add("pageSize", pageSize);

                // Adicionar condições apenas com valores validados
                if (tipoUsuarioId.HasValue)
                {
                    // Validar que o valor está no range válido (1-4)
                    if (tipoUsuarioId.Value >= 1 && tipoUsuarioId.Value <= 4)
                    {
                        whereConditions.Add("TipoUsuarioId = @tipoUsuarioId");
                        parameters.Add("tipoUsuarioId", tipoUsuarioId.Value);
                    }
                }

                if (ativo.HasValue)
                {
                    whereConditions.Add("Ativo = @ativo");
                    parameters.Add("ativo", ativo.Value);
                }

                // Construir WHERE clause de forma segura (apenas strings hardcoded)
                var whereClause = string.Join(" AND ", whereConditions);

                // SQL fixo com parâmetros - seguro contra SQL Injection
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
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Erro ao listar usuários no banco de dados. TenantId: {tenantId}, Erro: {ex.Message}", ex);
            }
        }

        public async Task<Usuario> CriarAsync(Usuario usuario, CancellationToken ct)
        {
            // Validações antes de inserir
            if (usuario.TenantId <= 0)
                throw new ArgumentException("TenantId deve ser maior que zero.", nameof(usuario));
            
            if (string.IsNullOrWhiteSpace(usuario.Login))
                throw new ArgumentException("Login é obrigatório.", nameof(usuario));
            
            if (string.IsNullOrWhiteSpace(usuario.NomeCompleto))
                throw new ArgumentException("NomeCompleto é obrigatório.", nameof(usuario));
            
            if (usuario.HashSenha == null || usuario.HashSenha.Length == 0)
                throw new ArgumentException("HashSenha é obrigatório.", nameof(usuario));
            
            if (usuario.SaltSenha == null || usuario.SaltSenha.Length == 0)
                throw new ArgumentException("SaltSenha é obrigatório.", nameof(usuario));

            // Garantir que a conexão está aberta
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

            // Usar parâmetros explícitos para evitar problemas de mapeamento
            var parameters = new DynamicParameters();
            parameters.Add("TenantId", usuario.TenantId, DbType.Int32);
            parameters.Add("TipoUsuarioId", (byte)usuario.TipoUsuarioId, DbType.Byte);
            parameters.Add("Login", usuario.Login?.Trim(), DbType.String, size: 100);
            parameters.Add("NomeCompleto", usuario.NomeCompleto?.Trim(), DbType.String, size: 200);
            parameters.Add("Email", string.IsNullOrWhiteSpace(usuario.Email) ? (object)DBNull.Value : usuario.Email.Trim(), DbType.String, size: 200);
            parameters.Add("Telefone", string.IsNullOrWhiteSpace(usuario.Telefone) ? (object)DBNull.Value : usuario.Telefone.Trim(), DbType.String, size: 50);
            parameters.Add("HashSenha", usuario.HashSenha, DbType.Binary);
            parameters.Add("SaltSenha", usuario.SaltSenha, DbType.Binary);
            parameters.Add("PrecisaTrocarSenha", usuario.PrecisaTrocarSenha, DbType.Boolean);
            parameters.Add("Ativo", usuario.Ativo, DbType.Boolean);
            parameters.Add("DataCriacao", usuario.DataCriacao, DbType.DateTime2);
            parameters.Add("Excluido", usuario.Excluido, DbType.Boolean);

            const string sql = @"
                INSERT INTO core.Usuario 
                    (TenantId, TipoUsuarioId, Login, NomeCompleto, Email, Telefone, 
                     HashSenha, SaltSenha, PrecisaTrocarSenha, Ativo, DataCriacao, Excluido)
                VALUES 
                    (@TenantId, @TipoUsuarioId, @Login, @NomeCompleto, @Email, @Telefone,
                     @HashSenha, @SaltSenha, @PrecisaTrocarSenha, @Ativo, @DataCriacao, @Excluido);
                SELECT CAST(SCOPE_IDENTITY() as int);";

            try
            {
                var usuarioId = await _db.QuerySingleAsync<int>(
                    new CommandDefinition(sql, parameters, cancellationToken: ct));

                if (usuarioId <= 0)
                {
                    throw new InvalidOperationException("Falha ao criar usuário. ID retornado é inválido.");
                }

                usuario.UsuarioId = usuarioId;
                return usuario;
            }
            catch (Exception ex)
            {
                // Log do erro para diagnóstico com mais detalhes
                var errorDetails = $"Erro ao criar usuário no banco de dados. " +
                    $"TenantId: {usuario.TenantId}, Login: {usuario.Login}, " +
                    $"TipoUsuarioId: {usuario.TipoUsuarioId}, " +
                    $"HashSenha: {(usuario.HashSenha != null ? usuario.HashSenha.Length + " bytes" : "NULL")}, " +
                    $"SaltSenha: {(usuario.SaltSenha != null ? usuario.SaltSenha.Length + " bytes" : "NULL")}, " +
                    $"Erro: {ex.Message}";
                
                throw new InvalidOperationException(errorDetails, ex);
            }
        }

        public async Task<Usuario> AtualizarAsync(Usuario usuario, CancellationToken ct)
        {
            // Garantir que a conexão está aberta
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

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
            // Garantir que a conexão está aberta
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

            // Log para debug
            System.Diagnostics.Debug.WriteLine($"[AlterarAtivacaoAsync] UsuarioId: {usuarioId}, Ativo: {ativo} (tipo: {ativo.GetType().Name})");

            // Converter bool para int (0 ou 1) para garantir compatibilidade com SQL Server BIT
            var ativoInt = ativo ? 1 : 0;
            
            const string sql = @"
                UPDATE core.Usuario 
                SET Ativo = @ativoInt,
                    DataAtualizacao = GETUTCDATE()
                WHERE UsuarioId = @usuarioId AND Excluido = 0";

            var parameters = new DynamicParameters();
            parameters.Add("usuarioId", usuarioId, DbType.Int32);
            // Usar int (0 ou 1) em vez de bool para garantir compatibilidade
            parameters.Add("ativoInt", ativoInt, DbType.Int32);

            var rowsAffected = await _db.ExecuteAsync(
                new CommandDefinition(sql, parameters, cancellationToken: ct));
            
            System.Diagnostics.Debug.WriteLine($"[AlterarAtivacaoAsync] Rows affected: {rowsAffected}");
            
            if (rowsAffected == 0)
            {
                throw new InvalidOperationException($"Nenhum usuário foi atualizado. UsuarioId: {usuarioId}, Ativo: {ativo}. Verifique se o usuário existe e não está excluído.");
            }
        }

        public async Task AtualizarSenhaAsync(int usuarioId, byte[] hash, byte[] salt, CancellationToken ct)
        {
            // Garantir que a conexão está aberta
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

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
            // Garantir que a conexão está aberta
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

            const string sql = @"
                SELECT COUNT(*) FROM core.Usuario 
                WHERE TenantId = @tenantId AND Login = @login AND Excluido = 0";

            var count = await _db.QuerySingleAsync<int>(
                new CommandDefinition(sql, new { tenantId, login }, cancellationToken: ct));

            return count > 0;
        }
    }
}
