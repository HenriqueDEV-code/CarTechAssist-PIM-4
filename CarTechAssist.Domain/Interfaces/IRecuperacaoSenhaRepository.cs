namespace CarTechAssist.Domain.Interfaces
{
    public interface IRecuperacaoSenhaRepository
    {
        Task<Entities.RecuperacaoSenha?> ObterPorCodigoAsync(string codigo, CancellationToken ct);
        Task<Entities.RecuperacaoSenha?> ObterPorUsuarioAsync(int tenantId, int usuarioId, CancellationToken ct);
        Task<long> CriarAsync(Entities.RecuperacaoSenha recuperacao, CancellationToken ct);
        Task MarcarComoUsadoAsync(long recuperacaoSenhaId, CancellationToken ct);
        Task LimparExpiradasAsync(int tenantId, CancellationToken ct);
    }
}

