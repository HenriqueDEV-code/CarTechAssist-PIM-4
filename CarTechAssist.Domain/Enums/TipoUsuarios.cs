// Tipos de usuarios em sistema CarTechAssist



using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarTechAssist.Domain.Enums
{
    public enum TipoUsuarios : byte
    {
        Cliente = 1,       // Usuario comum
        Tecnico = 2,       // Usuario tecnico/especialista/Atendente
        Administrador = 3, // Usuario administrador do sistema
        Bot = 4            // Usuario automatizado (bot)
    }
}
