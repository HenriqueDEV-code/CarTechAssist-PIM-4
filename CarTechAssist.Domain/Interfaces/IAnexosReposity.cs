using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CarTechAssist.Domain.Entities;

namespace CarTechAssist.Domain.Interfaces
{
    public interface IAnexosReposity
    {
        Task<long> AdicionarAsync(ChamadoAnexo anexo, CancellationToken ct);
        Task<IReadOnlyList<ChamadoAnexo>> ListarPorChamadoAsync(int chamadoId, CancellationToken ct);
    }
}
