using CarTechAssist.Domain.Entities;

namespace CarTechAssist.Domain.Interfaces
{
    public interface IRefreshTokenRepository
    {
        Task<RefreshToken?> ObterPorTokenAsync(string token, CancellationToken ct);
        Task<RefreshToken> CriarAsync(RefreshToken refreshToken, CancellationToken ct);
        Task RevogarAsync(long refreshTokenId, CancellationToken ct);
        Task RevogarTodosDoUsuarioAsync(int usuarioId, CancellationToken ct);
        Task LimparExpiradosAsync(CancellationToken ct);
    }
}

