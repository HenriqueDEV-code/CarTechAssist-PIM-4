using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarTechAssist.Domain.Enums
{
    public enum CanalAtendimento : byte
    {
        Web = 1,          // Atendimento via web
        Desktop = 2,      // Atendimento via aplicativo desktop
        Mobile = 3,       // Atendimento via aplicativo mobile
        Chatbot = 4,      // Atendimento via chatbot
    }
    
}
