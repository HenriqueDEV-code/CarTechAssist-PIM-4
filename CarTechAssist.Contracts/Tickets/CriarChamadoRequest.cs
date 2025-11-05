using System;

namespace CarTechAssist.Contracts.Tickets
{
    /// <summary>
    /// Request para criação de um novo chamado de suporte
    /// </summary>
    /// <param name="Titulo">Título do chamado (obrigatório, máximo 200 caracteres)</param>
    /// <param name="Descricao">Descrição detalhada do problema (obrigatório, máximo 10.000 caracteres)</param>
    /// <param name="CategoriaId">ID da categoria do chamado (obrigatório, deve existir no banco)</param>
    /// <param name="PrioridadeId">Prioridade do chamado: 1=Baixa, 2=Média, 3=Alta, 4=Urgente (obrigatório)</param>
    /// <param name="CanalId">Canal de atendimento: 1=Web, 2=Desktop, 3=Mobile, 4=Chatbot (obrigatório)</param>
    /// <param name="SolicitanteUsuarioId">ID do usuário solicitante (será preenchido automaticamente pelo controller com o usuário autenticado - não precisa enviar)</param>
    /// <param name="ResponsavelUsuarioId">ID do usuário responsável (opcional, pode ser atribuído posteriormente)</param>
    /// <param name="SLA_EstimadoFim">Data/hora estimada para conclusão do chamado (calculada automaticamente baseado na prioridade)</param>
    public record CriarChamadoRequest(
        string Titulo,
        string Descricao, // CORREÇÃO: Tornado obrigatório
        int CategoriaId, // CORREÇÃO: Tornado obrigatório
        byte PrioridadeId,
        byte CanalId,
        int SolicitanteUsuarioId, // Será preenchido automaticamente pelo controller
        int? ResponsavelUsuarioId,
        DateTime? SLA_EstimadoFim // Calculado automaticamente baseado na prioridade
    );
}


