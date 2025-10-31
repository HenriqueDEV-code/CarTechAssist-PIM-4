using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CarTechAssist.Domain.Entities;

namespace CarTechAssist.Domain.Interfaces
{
    public interface ICategoriasRepository
    {
        Task<IReadOnlyList<CategoriaChamado>> ListarAtivasAsync(int tenantId, CancellationToken ct);
    }
}
