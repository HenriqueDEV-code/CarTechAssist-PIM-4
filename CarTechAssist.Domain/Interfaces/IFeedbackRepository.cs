using CarTechAssist.Domain.Entities;
using CarTechAssist.Domain.Enums;

namespace CarTechAssist.Domain.Interfaces
{
    public interface IFeedbackRepository
    {
        Task<long> AdicionarAsync(
            int tenantId,
            long? chamadoId,
            long? interacaoId,
            int? dadoPorUsuarioId,
            IAFeedbackScore score,
            string? comentario,
            CancellationToken ct);

        Task<IAFeedback?> ObterPorChamadoAsync(
            long chamadoId,
            int tenantId,
            CancellationToken ct);
    }
}

