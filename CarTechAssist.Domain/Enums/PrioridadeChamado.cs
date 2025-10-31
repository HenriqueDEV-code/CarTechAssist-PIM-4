using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarTechAssist.Domain.Enums
{
    public enum PrioridadeChamado : byte
    {
        Baixa = 1,      // Prioridade baixa
        Media = 2,      // Prioridade media
        Alta = 3,       // Prioridade alta
        Urgente = 4     // Prioridade urgente
    }
    
}
