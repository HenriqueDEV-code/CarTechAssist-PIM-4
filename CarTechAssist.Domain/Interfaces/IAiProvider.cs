using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CarTechAssist.Domain.Entities;

namespace CarTechAssist.Domain.Interfaces
{
    public interface IAiProvider
    {

        Task
            <(string Provedor, string Modelo, string Mensagem, decimal? Confianca,
            string? ResumoRaciocinio, int? InputTokens, int? outputTokens, decimal? CustoUsd)>
            ResponderAsync(string prompt, CancellationToken ct);
    }
}
