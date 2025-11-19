using System.Data;
using Dapper;
using CarTechAssist.Domain.Entities;
using CarTechAssist.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace CarTechAssist.Infrastruture.Repositories
{
    public class IARunLogRepository : IIARunLogRepository
    {
        private readonly IDbConnection _db;

        public IARunLogRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<long> CriarAsync(IARunLog runLog, CancellationToken ct)
        {
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

            const string sql = @"
                INSERT INTO ia.IARunLog 
                    (TenantId, ChamadoId, InteracaoId, Provedor, Modelo, PromptHash, 
                     InputTokens, OutputTokens, LatenciaMs, CustoUSD, Confianca, TipoResultado, DataCriacao)
                OUTPUT INSERTED.IARunId
                VALUES 
                    (@tenantId, @chamadoId, @interacaoId, @provedor, @modelo, @promptHash,
                     @inputTokens, @outputTokens, @latenciaMs, @custoUsd, @confianca, @tipoResultado, GETUTCDATE())";

            var parameters = new
            {
                tenantId = runLog.TenantId,
                chamadoId = runLog.ChamadoId ?? (object)DBNull.Value,
                interacaoId = runLog.InteracaoId ?? (object)DBNull.Value,
                provedor = runLog.Provedor,
                modelo = runLog.Modelo,
                promptHash = runLog.PromptHash ?? (object)DBNull.Value,
                inputTokens = runLog.InputTokens ?? (object)DBNull.Value,
                outputTokens = runLog.OutputTokens ?? (object)DBNull.Value,
                latenciaMs = runLog.LatenciaMs ?? (object)DBNull.Value,
                custoUsd = runLog.CustoUSD ?? (object)DBNull.Value,
                confianca = runLog.Confianca ?? (object)DBNull.Value,
                tipoResultado = runLog.TipoResultado ?? (object)DBNull.Value
            };

            var runId = await _db.QuerySingleAsync<long>(
                new CommandDefinition(sql, parameters, cancellationToken: ct));

            return runId;
        }
    }
}

