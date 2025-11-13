using System.Data;
using Dapper;
using CarTechAssist.Domain.Entities;
using CarTechAssist.Domain.Enums;
using CarTechAssist.Domain.Interfaces;

namespace CarTechAssist.Infrastruture.Repositories
{
    public class FeedbackRepository : IFeedbackRepository
    {
        private readonly IDbConnection _db;

        public FeedbackRepository(IDbConnection db)
        {
            _db = db;
        }

        public async Task<long> AdicionarAsync(
            int tenantId,
            long? chamadoId,
            long? interacaoId,
            int? dadoPorUsuarioId,
            IAFeedbackScore score,
            string? comentario,
            CancellationToken ct)
        {
            // Garantir que a conexão está aberta
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

            const string sql = @"
                INSERT INTO ia.IAFeedback (TenantId, ChamadoId, InteracaoId, DadoPorUsuarioId, Score, Comentario, DataCriacao)
                OUTPUT INSERTED.FeedbackId
                VALUES (@tenantId, @chamadoId, @interacaoId, @dadoPorUsuarioId, @score, @comentario, GETUTCDATE())";

            var parameters = new
            {
                tenantId,
                chamadoId = chamadoId ?? (object)DBNull.Value,
                interacaoId = interacaoId ?? (object)DBNull.Value,
                dadoPorUsuarioId = dadoPorUsuarioId ?? (object)DBNull.Value,
                score = (byte)score,
                comentario = comentario ?? (object)DBNull.Value
            };

            var feedbackId = await _db.QuerySingleAsync<long>(
                new CommandDefinition(sql, parameters, cancellationToken: ct));

            return feedbackId;
        }

        public async Task<IAFeedback?> ObterPorChamadoAsync(
            long chamadoId,
            int tenantId,
            CancellationToken ct)
        {
            // Garantir que a conexão está aberta
            if (_db.State != ConnectionState.Open)
            {
                _db.Open();
            }

            const string sql = @"
                SELECT TOP 1 FeedbackId, TenantId, ChamadoId, InteracaoId, 
                       DadoPorUsuarioId, Score, Comentario, DataCriacao
                FROM ia.IAFeedback 
                WHERE ChamadoId = @chamadoId AND TenantId = @tenantId
                ORDER BY DataCriacao DESC";

            var feedback = await _db.QueryFirstOrDefaultAsync<IAFeedback>(
                new CommandDefinition(sql, new { chamadoId, tenantId }, cancellationToken: ct));

            return feedback;
        }
    }
}

