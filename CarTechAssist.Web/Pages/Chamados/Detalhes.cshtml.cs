using CarTechAssist.Contracts.Tickets;
using CarTechAssist.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarTechAssist.Web.Pages.Chamados
{
    /// <summary>
    /// Model da página de detalhes do chamado.
    /// Gerencia a exibição de informações do chamado, chat/mensagens e anexos.
    /// </summary>
    public class DetalhesModel : PageModel
    {
        private readonly ChamadosService _chamadosService;
        private readonly ILogger<DetalhesModel> _logger;

        // Propriedades públicas para uso na view
        public ChamadoDetailDto? Chamado { get; set; }              // Dados do chamado
        public IReadOnlyList<InteracaoDto>? Interacoes { get; set; } // Lista de mensagens/interações
        public IReadOnlyList<AnexoDto>? Anexos { get; set; }        // Lista de anexos
        public string? ErrorMessage { get; set; }                    // Mensagem de erro para exibição
        public int TenantId { get; set; }                            // ID do tenant (multi-tenant)
        public int UsuarioId { get; set; }                           // ID do usuário logado
        public byte TipoUsuarioId { get; set; }                      // Tipo do usuário (1=Cliente, 2=Técnico, 3=Admin)

        /// <summary>
        /// Construtor - Injeção de dependências
        /// </summary>
        public DetalhesModel(ChamadosService chamadosService, ILogger<DetalhesModel> logger)
        {
            _chamadosService = chamadosService;
            _logger = logger;
        }


        /// <summary>
        /// Handler GET - Carrega os dados do chamado, interações e anexos para exibição na página.
        /// </summary>
        /// <param name="id">ID do chamado a ser exibido</param>
        /// <param name="ct">Token de cancelamento</param>
        /// <returns>Página de detalhes ou redirecionamento em caso de erro</returns>
        public async Task<IActionResult> OnGetAsync(long id, CancellationToken ct = default)
        {
            // Validação de autenticação - verifica se o usuário está logado
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            // Recupera informações do usuário da sessão (multi-tenant)
            var tenantIdStr = HttpContext.Session.GetString("TenantId");
            var usuarioIdStr = HttpContext.Session.GetString("UsuarioId");
            var tipoUsuarioIdStr = HttpContext.Session.GetString("TipoUsuarioId");

            // Parse e validação do TenantId (padrão: 1)
            if (!int.TryParse(tenantIdStr, out var tenantId))
                tenantId = 1;
            TenantId = tenantId;

            // Parse e validação do UsuarioId (obrigatório)
            if (!int.TryParse(usuarioIdStr, out var usuarioId))
            {
                return RedirectToPage("/Login");
            }
            UsuarioId = usuarioId;

            // Parse e validação do TipoUsuarioId (padrão: 1 = Cliente)
            if (!byte.TryParse(tipoUsuarioIdStr, out var tipoUsuarioId))
                tipoUsuarioId = 1;
            TipoUsuarioId = tipoUsuarioId;

            try
            {
                // Carrega os dados do chamado da API
                Chamado = await _chamadosService.ObterAsync(id, ct);
                if (Chamado == null)
                {
                    ErrorMessage = "Chamado não encontrado.";
                    return RedirectToPage("/Chamados");
                }

                // Carrega as interações (mensagens/chat) do chamado
                Interacoes = await _chamadosService.ListarInteracoesAsync(id, ct);
                
                // Carrega os anexos do chamado
                Anexos = await _chamadosService.ListarAnexosAsync(id, ct);
            }
            // Tratamento de erros HTTP da API
            catch (System.Net.Http.HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Erro HTTP ao carregar detalhes do chamado {ChamadoId}", id);

                // Tratamento específico por código de status HTTP
                if (httpEx.Data.Contains("StatusCode") && httpEx.Data["StatusCode"] is System.Net.HttpStatusCode statusCode)
                {
                    if (statusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        // Token expirado ou inválido - redireciona para login
                        return RedirectToPage("/Login");
                    }
                    else if (statusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // Chamado não existe - redireciona para lista
                        ErrorMessage = "Chamado não encontrado.";
                        return RedirectToPage("/Chamados");
                    }
                    else if (statusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        // Usuário não tem permissão - exibe mensagem
                        ErrorMessage = "Você não tem permissão para acessar este chamado.";
                    }
                    else
                    {
                        // Outros erros HTTP - tenta extrair mensagem da API
                        var apiMessage = httpEx.Data.Contains("Message") ? httpEx.Data["Message"]?.ToString() : null;
                        ErrorMessage = !string.IsNullOrEmpty(apiMessage) ? apiMessage : $"Erro ao carregar chamado: {httpEx.Message}";
                    }
                }
                else
                {
                    ErrorMessage = $"Erro ao carregar chamado: {httpEx.Message}";
                }
            }
            catch (Exception ex)
            {
                // Tratamento de erros inesperados
                _logger.LogError(ex, "Erro inesperado ao carregar detalhes do chamado {ChamadoId}", id);
                ErrorMessage = "Erro ao carregar chamado. Tente novamente.";
            }

            return Page();
        }

        /// <summary>
        /// Handler POST - Adiciona uma nova interação (mensagem) ao chamado.
        /// Chamado via AJAX quando o usuário envia uma mensagem no chat.
        /// </summary>
        /// <param name="id">ID do chamado</param>
        /// <param name="mensagem">Texto da mensagem a ser enviada</param>
        /// <param name="ct">Token de cancelamento</param>
        /// <returns>Redirecionamento para a página de detalhes ou erro</returns>
        [Microsoft.AspNetCore.Mvc.ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAdicionarInteracaoAsync(long id, [FromForm] string mensagem, CancellationToken ct = default)
        {
            // Validação de autenticação
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            // Validação da mensagem
            if (string.IsNullOrWhiteSpace(mensagem))
            {
                return BadRequest("Mensagem não pode estar vazia.");
            }

            try
            {
                // Cria o request e envia para a API
                var request = new AdicionarInteracaoRequest(mensagem);
                await _chamadosService.AdicionarInteracaoAsync(id, request, ct);
                
                // Redireciona para recarregar a página com a nova mensagem
                return RedirectToPage("/Chamados/Detalhes", new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao adicionar interação ao chamado {ChamadoId}", id);
                return BadRequest("Erro ao enviar mensagem. Tente novamente.");
            }
        }

        /// <summary>
        /// Handler POST - Faz upload de um anexo para o chamado.
        /// Chamado via AJAX quando o usuário envia um arquivo.
        /// Validações de tamanho e tipo são feitas na API.
        /// </summary>
        /// <param name="id">ID do chamado</param>
        /// <param name="arquivo">Arquivo a ser enviado (IFormFile)</param>
        /// <param name="ct">Token de cancelamento</param>
        /// <returns>Redirecionamento para a página de detalhes ou erro</returns>
        [Microsoft.AspNetCore.Mvc.ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostUploadAnexoAsync(long id, IFormFile arquivo, CancellationToken ct = default)
        {
            // Validação de autenticação
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            // Validação do arquivo
            if (arquivo == null || arquivo.Length == 0)
            {
                return BadRequest("Arquivo não fornecido.");
            }

            try
            {
                // Envia o arquivo para a API (validações de tamanho/tipo são feitas lá)
                await _chamadosService.UploadAnexoAsync(id, arquivo, ct);
                
                // Redireciona para recarregar a página com o novo anexo
                return RedirectToPage("/Chamados/Detalhes", new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao fazer upload de anexo no chamado {ChamadoId}", id);
                return BadRequest($"Erro ao enviar anexo: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler GET - Faz download de um anexo do chamado.
        /// Retorna o arquivo como stream para download no navegador.
        /// </summary>
        /// <param name="id">ID do chamado</param>
        /// <param name="anexoId">ID do anexo a ser baixado</param>
        /// <param name="ct">Token de cancelamento</param>
        /// <returns>Arquivo para download ou NotFound</returns>
        public async Task<IActionResult> OnGetDownloadAnexoAsync(long id, long anexoId, CancellationToken ct = default)
        {
            // Validação de autenticação
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            try
            {
                // Obtém o stream do arquivo da API
                var stream = await _chamadosService.DownloadAnexoAsync(id, anexoId, ct);
                
                // Busca os metadados do anexo para obter nome e content-type
                var anexos = await _chamadosService.ListarAnexosAsync(id, ct);
                var anexo = anexos?.FirstOrDefault(a => a.AnexoId == anexoId);
                
                if (anexo == null)
                {
                    return NotFound();
                }

                // Retorna o arquivo para download
                return File(stream, anexo.ContentType ?? "application/octet-stream", anexo.NomeArquivo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao fazer download do anexo {AnexoId} do chamado {ChamadoId}", anexoId, id);
                return NotFound();
            }
        }

        /// <summary>
        /// Retorna a classe CSS do Bootstrap para o badge de status do chamado.
        /// Usado na view para estilizar visualmente o status.
        /// </summary>
        /// <param name="statusId">ID do status (1=Aberto, 2=Em Andamento, 3=Pendente, 4=Resolvido, 5=Fechado, 6=Cancelado)</param>
        /// <returns>Classe CSS do Bootstrap para o badge</returns>
        public string GetStatusBadgeClass(byte statusId)
        {
            return statusId switch
            {
                1 => "bg-info text-white",      // Aberto - azul claro
                2 => "bg-primary text-white",   // Em Andamento
                3 => "bg-warning text-dark",    // Pendente
                4 => "bg-success text-white",   // Resolvido
                5 => "bg-secondary text-white", // Fechado
                6 => "bg-danger text-white",    // Cancelado
                _ => "bg-secondary text-white"  // Padrão
            };
        }

        /// <summary>
        /// Retorna a classe CSS do Bootstrap para o badge de prioridade do chamado.
        /// Usado na view para estilizar visualmente a prioridade.
        /// </summary>
        /// <param name="prioridadeId">ID da prioridade (1=Baixa, 2=Média, 3=Alta, 4=Urgente)</param>
        /// <returns>Classe CSS do Bootstrap para o badge</returns>
        public string GetPrioridadeBadgeClass(byte prioridadeId)
        {
            return prioridadeId switch
            {
                1 => "bg-success text-white",   // Baixa - verde
                2 => "bg-info text-white",      // Média - azul
                3 => "bg-warning text-dark",    // Alta - amarelo
                4 => "bg-warning text-dark",    // Urgente - amarelo conforme imagem
                _ => "bg-secondary text-white"  // Padrão
            };
        }

        /// <summary>
        /// Formata o tamanho do arquivo em bytes para uma representação legível (B, KB, MB, GB).
        /// </summary>
        /// <param name="bytes">Tamanho do arquivo em bytes</param>
        /// <returns>String formatada (ex: "1.5 MB")</returns>
        public string FormatFileSize(long? bytes)
        {
            if (!bytes.HasValue || bytes.Value == 0)
                return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes.Value;
            int order = 0;
            
            // Converte para a unidade apropriada (KB, MB, GB)
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}

