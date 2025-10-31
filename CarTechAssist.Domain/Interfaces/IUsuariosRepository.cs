using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CarTechAssist.Domain.Entities;

namespace CarTechAssist.Domain.Interfaces
{
    public interface IUsuariosRepository
    {
        Task<Usuario?> ObterPorLoginAsync(int tenantId, string login, CancellationToken ct);
        Task<Usuario?> ObterPorIdAsync(int usuarioId, CancellationToken ct);
        
        Task<(IReadOnlyList<Usuario> Items, int Total)> ListarAsync(
            int tenantId,
            byte? tipoUsuarioId,
            bool? ativo,
            int page,
            int pageSize,
            CancellationToken ct);

        Task<Usuario> CriarAsync(Usuario usuario, CancellationToken ct);
        Task<Usuario> AtualizarAsync(Usuario usuario, CancellationToken ct);
        Task AlterarAtivacaoAsync(int usuarioId, bool ativo, CancellationToken ct);
        Task AtualizarSenhaAsync(int usuarioId, byte[] hash, byte[] salt, CancellationToken ct);
        Task<bool> ExisteLoginAsync(int tenantId, string login, CancellationToken ct);
    }
}
