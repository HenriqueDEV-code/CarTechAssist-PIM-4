using CarTechAssist.Contracts.Tickets;
using CarTechAssist.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Linq;

namespace CarTechAssist.Web.Pages.Chamados
{
    /// <summary>
    /// Modelo da página Razor para criação de novos chamados de suporte.
    /// Gerencia o formulário de criação e validação dos dados do chamado.
    /// </summary>
    public class CriarModel : PageModel
    {
        // Serviços injetados via dependência para comunicação com a API
        private readonly ChamadosService _chamadosService;      // Serviço para operações de chamados
        private readonly CategoriasService _categoriasService;   // Serviço para listagem de categorias
        private readonly UsuariosService _usuariosService;       // Serviço para listagem de usuários
        private readonly ILogger<CriarModel> _logger;            // Logger para registro de erros e eventos

        /// <summary>
        /// Propriedade vinculada ao formulário que contém os dados do chamado a ser criado.
        /// Os valores padrão são definidos na inicialização.
        /// </summary>
        [BindProperty]
        public CriarChamadoRequest ChamadoRequest { get; set; } = new(
            Titulo: string.Empty,
            Descricao: string.Empty, // CORREÇÃO: Tornado obrigatório
            CategoriaId: 0, // CORREÇÃO: Será validado antes de enviar
            PrioridadeId: 2, // Média
            CanalId: 1, // Web
            SolicitanteUsuarioId: 0, // Será preenchido automaticamente
            ResponsavelUsuarioId: null,
            SLA_EstimadoFim: null // Será calculado automaticamente
        );

        // Propriedades públicas para exibição na view
        public IReadOnlyList<CategoriaDto>? Categorias { get; set; }  // Lista de categorias disponíveis para seleção
        public string? ErrorMessage { get; set; }                      // Mensagem de erro a ser exibida na view
        public string? SuccessMessage { get; set; }                    // Mensagem de sucesso a ser exibida na view
        public int TenantId { get; set; }                              // ID do tenant (organização) do usuário logado
        public int UsuarioId { get; set; }                             // ID do usuário logado
        public byte TipoUsuarioId { get; set; }                        // Tipo do usuário: 1=Cliente, 2=Técnico, 3=Admin
        public IReadOnlyList<CarTechAssist.Contracts.Usuarios.UsuarioDto>? Usuarios { get; set; }  // Lista de usuários (para Técnico/Admin)
        public IReadOnlyList<CarTechAssist.Contracts.Usuarios.UsuarioDto>? Tecnicos { get; set; }  // Lista de técnicos (para atribuição de responsável)

        /// <summary>
        /// Construtor da classe. Recebe os serviços via injeção de dependência.
        /// </summary>
        /// <param name="chamadosService">Serviço para operações de chamados</param>
        /// <param name="categoriasService">Serviço para listagem de categorias</param>
        /// <param name="usuariosService">Serviço para listagem de usuários</param>
        /// <param name="logger">Logger para registro de eventos</param>
        public CriarModel(
            ChamadosService chamadosService,
            CategoriasService categoriasService,
            UsuariosService usuariosService,
            ILogger<CriarModel> logger)
        {
            _chamadosService = chamadosService;
            _categoriasService = categoriasService;
            _usuariosService = usuariosService;
            _logger = logger;
        }

        /// <summary>
        /// Método executado quando a página é carregada via GET.
        /// Carrega os dados necessários para preencher o formulário (categorias, usuários, etc.)
        /// e configura os valores padrão baseados no tipo de usuário logado.
        /// </summary>
        /// <param name="ct">Token de cancelamento para operações assíncronas</param>
        /// <returns>Retorna a página ou redireciona para login se não autenticado</returns>
        public async Task<IActionResult> OnGetAsync(CancellationToken ct = default)
        {
            // Verifica se o usuário está autenticado através do token na sessão
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            // Obtém o ID do tenant (organização) da sessão
            var tenantIdStr = HttpContext.Session.GetString("TenantId");
            var usuarioIdStr = HttpContext.Session.GetString("UsuarioId");

            // Parse do TenantId com valor padrão 1 se não conseguir converter
            if (!int.TryParse(tenantIdStr, out var tenantId))
                tenantId = 1;
            TenantId = tenantId;

            // Obtém o ID do usuário logado. Se não conseguir, redireciona para login
            if (!int.TryParse(usuarioIdStr, out var usuarioId))
            {
                return RedirectToPage("/Login");
            }
            UsuarioId = usuarioId;

            // Obtém o tipo de usuário da sessão (1=Cliente, 2=Técnico, 3=Admin)
            var tipoUsuarioIdStr = HttpContext.Session.GetString("TipoUsuarioId");
            if (byte.TryParse(tipoUsuarioIdStr, out var tipoUsuarioId))
            {
                TipoUsuarioId = tipoUsuarioId;
            }
            else
            {
                TipoUsuarioId = 1; // Padrão: Cliente
            }

            // Se for Cliente, define automaticamente o solicitante como o próprio usuário logado
            if (TipoUsuarioId == 1) // Cliente
            {
                ChamadoRequest = ChamadoRequest with { SolicitanteUsuarioId = usuarioId };
            }
            else
            {
                // Se for Técnico ou Admin, carrega listas de usuários e técnicos para seleção
                try
                {
                    // Carrega todos os usuários ativos para seleção de solicitante
                    var usuariosResult = await _usuariosService.ListarAsync(tipo: null, ativo: true, page: 1, pageSize: 1000, ct);
                    Usuarios = usuariosResult?.Items;

                    // Carrega apenas técnicos ativos para seleção de responsável
                    var tecnicosResult = await _usuariosService.ListarAsync(tipo: 2, ativo: true, page: 1, pageSize: 1000, ct);
                    Tecnicos = tecnicosResult?.Items;

                    // Se for Técnico ou Admin, define automaticamente o responsável como o próprio usuário logado
                    if (TipoUsuarioId == 2 || TipoUsuarioId == 3) // Técnico ou Admin
                    {
                        ChamadoRequest = ChamadoRequest with { ResponsavelUsuarioId = usuarioId };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao carregar usuários");
                }
            }

            // Carrega todas as categorias disponíveis para preencher o dropdown
            try
            {
                Categorias = await _categoriasService.ListarAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar categorias");
            }

            return Page();
        }

        /// <summary>
        /// Método executado quando o formulário é submetido via POST.
        /// Valida os dados do chamado, preenche campos automáticos baseados no tipo de usuário,
        /// e cria o chamado através do serviço. Em caso de sucesso, redireciona para a página de detalhes.
        /// </summary>
        /// <param name="ct">Token de cancelamento para operações assíncronas</param>
        /// <returns>Retorna a página com erros ou redireciona para detalhes em caso de sucesso</returns>
        public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
        {
            // Verifica autenticação do usuário
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            // Obtém o ID do usuário logado
            var usuarioIdStr = HttpContext.Session.GetString("UsuarioId");
            if (!int.TryParse(usuarioIdStr, out var usuarioId))
            {
                return RedirectToPage("/Login");
            }

            // Validação dos campos obrigatórios do formulário
            if (string.IsNullOrWhiteSpace(ChamadoRequest.Titulo))
            {
                ModelState.AddModelError(nameof(ChamadoRequest.Titulo), "Título é obrigatório.");
            }

            if (string.IsNullOrWhiteSpace(ChamadoRequest.Descricao))
            {
                ModelState.AddModelError(nameof(ChamadoRequest.Descricao), "Descrição é obrigatória.");
            }

            if (ChamadoRequest.CategoriaId <= 0)
            {
                ModelState.AddModelError(nameof(ChamadoRequest.CategoriaId), "Categoria é obrigatória. Selecione uma categoria.");
            }

            // Validação da prioridade (deve estar entre 1 e 4)
            if (ChamadoRequest.PrioridadeId < 1 || ChamadoRequest.PrioridadeId > 4)
            {
                ModelState.AddModelError(nameof(ChamadoRequest.PrioridadeId), "Prioridade inválida.");
            }

            // Validação do canal (deve estar entre 1 e 4)
            if (ChamadoRequest.CanalId < 1 || ChamadoRequest.CanalId > 4)
            {
                ModelState.AddModelError(nameof(ChamadoRequest.CanalId), "Canal inválido.");
            }

            // Se houver erros de validação, recarrega as categorias e retorna a página com os erros
            if (!ModelState.IsValid)
            {
                try
                {
                    Categorias = await _categoriasService.ListarAsync(ct);
                }
                catch { }
                ErrorMessage = "Por favor, corrija os erros no formulário.";
                return Page();
            }

            // Obtém o tipo de usuário para definir campos automáticos
            var tipoUsuarioIdStr = HttpContext.Session.GetString("TipoUsuarioId");
            byte tipoUsuarioId = 1;
            if (byte.TryParse(tipoUsuarioIdStr, out var tipo))
            {
                tipoUsuarioId = tipo;
            }

            // Define o solicitante automaticamente baseado no tipo de usuário
            if (tipoUsuarioId == 1)
            {
                // Cliente: sempre é o próprio usuário logado
                ChamadoRequest = ChamadoRequest with { SolicitanteUsuarioId = usuarioId };
            }
            else if (ChamadoRequest.SolicitanteUsuarioId <= 0)
            {
                // Técnico/Admin: se não foi selecionado, usa o próprio usuário
                ChamadoRequest = ChamadoRequest with { SolicitanteUsuarioId = usuarioId };
            }

            // Define o responsável automaticamente se for Técnico/Admin e não foi selecionado
            if ((tipoUsuarioId == 2 || tipoUsuarioId == 3) && !ChamadoRequest.ResponsavelUsuarioId.HasValue)
            {
                ChamadoRequest = ChamadoRequest with { ResponsavelUsuarioId = usuarioId };
            }

            // Tenta criar o chamado através do serviço
            try
            {
                var resultado = await _chamadosService.CriarAsync(ChamadoRequest, ct);
                if (resultado != null)
                {
                    // Sucesso: redireciona para a página de detalhes do chamado criado
                    SuccessMessage = "Chamado criado com sucesso!";
                    return RedirectToPage("/Chamados/Detalhes", new { id = resultado.ChamadoId });
                }
                else
                {
                    ErrorMessage = "Erro ao criar chamado. Tente novamente.";
                }
            }
            catch (Exception ex)
            {
                // Tratamento de erros específicos baseado na mensagem da exceção
                _logger.LogError(ex, "Erro ao criar chamado");

                if (ex.Message.Contains("Categoria") || ex.Message.Contains("categoria"))
                {
                    ErrorMessage = "Categoria inválida ou não encontrada. Por favor, selecione uma categoria válida.";
                    ModelState.AddModelError(nameof(ChamadoRequest.CategoriaId), "Categoria inválida.");
                }
                else if (ex.Message.Contains("Título") || ex.Message.Contains("título"))
                {
                    ErrorMessage = "Título inválido. Verifique o título e tente novamente.";
                    ModelState.AddModelError(nameof(ChamadoRequest.Titulo), ex.Message);
                }
                else if (ex.Message.Contains("Descrição") || ex.Message.Contains("descrição"))
                {
                    ErrorMessage = "Descrição inválida. Verifique a descrição e tente novamente.";
                    ModelState.AddModelError(nameof(ChamadoRequest.Descricao), ex.Message);
                }
                else
                {
                    ErrorMessage = $"Erro ao criar chamado: {ex.Message}. Verifique os dados e tente novamente.";
                }
            }

            // Recarrega as categorias para manter o formulário funcional em caso de erro
            try
            {
                Categorias = await _categoriasService.ListarAsync(ct);
            }
            catch { }

            return Page();
        }
    }
}

