using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarTechAssist.Domain.Enums
{
    public enum StatusChamado : byte
    {
        Aberto = 1,        // Chamado aberto
        EmAndamento = 2,   // Chamado em andamento
        Pendente = 3,      // Chamado pendente
        Resolvido = 4,     // Chamado resolvido
        Fechado = 5,       // Chamado fechado
        Cancelado = 6      // Chamado cancelado

    }
}
