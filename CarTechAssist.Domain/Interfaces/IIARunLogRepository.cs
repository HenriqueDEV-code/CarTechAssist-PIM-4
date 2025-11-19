using CarTechAssist.Domain.Entities;

namespace CarTechAssist.Domain.Interfaces
{
    public interface IIARunLogRepository
    {
        Task<long> CriarAsync(IARunLog runLog, CancellationToken ct);
    }
}

