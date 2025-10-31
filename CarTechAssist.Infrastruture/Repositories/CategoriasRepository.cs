using System.Data;
using Dapper;
using CarTechAssist.Domain.Entities;
using CarTechAssist.Domain.Interfaces;

namespace CarTechAssist.Infrastruture.Repositories
{
    public class CategoriasRepository : ICategoriasRepository
    {
        private readonly IDbConnection _db;

        public CategoriasRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<CategoriaChamado>> ListarAtivasAsync(int tenantId, CancellationToken ct)
        {
            const string sql = @"
                SELECT * FROM ref.CategoriaChamado 
                WHERE TenantId = @tenantId AND Ativo = 1 AND Excluido = 0
                ORDER BY Nome";

            var categorias = await _db.QueryAsync<CategoriaChamado>(
                new CommandDefinition(sql, new { tenantId }, cancellationToken: ct));

            return categorias.ToList();
        }
    }
}

