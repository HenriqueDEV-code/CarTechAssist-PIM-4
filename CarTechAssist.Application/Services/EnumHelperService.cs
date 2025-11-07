using CarTechAssist.Domain.Enums;

namespace CarTechAssist.Application.Services
{



    public class EnumHelperService
    {
        public static string GetStatusNome(StatusChamado statusId)
        {
            return statusId switch
            {
                StatusChamado.Aberto => "Aberto",
                StatusChamado.EmAndamento => "Em Andamento",
                StatusChamado.Pendente => "Pendente",
                StatusChamado.Resolvido => "Resolvido",
                StatusChamado.Fechado => "Fechado",
                StatusChamado.Cancelado => "Cancelado",
                _ => statusId.ToString()
            };
        }

        public static string GetPrioridadeNome(PrioridadeChamado prioridadeId)
        {
            return prioridadeId switch
            {
                PrioridadeChamado.Baixa => "Baixa",
                PrioridadeChamado.Media => "Média",
                PrioridadeChamado.Alta => "Alta",
                PrioridadeChamado.Urgente => "Urgente",
                _ => prioridadeId.ToString()
            };
        }

        public static string GetCanalNome(CanalAtendimento canalId)
        {
            return canalId switch
            {
                CanalAtendimento.Web => "Web",
                CanalAtendimento.Desktop => "Desktop",
                CanalAtendimento.Mobile => "Mobile",
                CanalAtendimento.Chatbot => "Chatbot",
                _ => canalId.ToString()
            };
        }
    }
}

