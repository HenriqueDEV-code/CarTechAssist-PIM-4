using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CarTechAssist.Domain.Entities;

namespace CarTechAssist.Domain.Interfaces
{
    public interface IAditoriaRepository
    {

        Task<long> RegistrarAsync(Auditoria audit, CancellationToken ct);
    }
}
