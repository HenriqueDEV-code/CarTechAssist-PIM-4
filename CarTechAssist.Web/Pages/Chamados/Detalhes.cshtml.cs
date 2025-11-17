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
        private readonly UsuariosService _usuariosService;
        private readonly ILogger<DetalhesModel> _logger;

        // Propriedades públicas para uso na view
        public ChamadoDetailDto? Chamado { get; set; }              // Dados do chamado
        public IReadOnlyList<InteracaoDto>? Interacoes { get; set; } // Lista de mensagens/interações
        public IReadOnlyList<AnexoDto>? Anexos { get; set; }        // Lista de anexos
        public string? ErrorMessage { get; set; }                    // Mensagem de erro para exibição
        public int TenantId { get; set; }                            // ID do tenant (multi-tenant)
        public int UsuarioId { get; set; }                           // ID do usuário logado
        public byte TipoUsuarioId { get; set; }                      // Tipo do usuário (1=Cliente, 2=Técnico, 3=Admin)
        public string? SolicitanteNome { get; set; }                 // Nome do usuário que abriu o chamado

        /// <summary>
        /// Construtor - Injeção de dependências
        /// </summary>
        public DetalhesModel(ChamadosService chamadosService, UsuariosService usuariosService, ILogger<DetalhesModel> logger)
        {
            _chamadosService = chamadosService;
            _usuariosService = usuariosService;
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

                // Busca o nome do usuário solicitante
                try
                {
                    var solicitante = await _usuariosService.ObterAsync(Chamado.SolicitanteUsuarioId, ct);
                    SolicitanteNome = solicitante?.NomeCompleto ?? "Usuário não encontrado";
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao buscar nome do solicitante {SolicitanteUsuarioId}", Chamado.SolicitanteUsuarioId);
                    SolicitanteNome = "Usuário não encontrado";
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
        /// Handler GET - Busca novas interações do chamado (para atualização automática).
        /// Retorna apenas interações com ID maior que o último ID conhecido.
        /// </summary>
        /// <param name="id">ID do chamado</param>
        /// <param name="ultimoInteracaoId">ID da última interação conhecida (opcional)</param>
        /// <param name="ct">Token de cancelamento</param>
        /// <returns>JSON com lista de novas interações</returns>
        public async Task<IActionResult> OnGetNovasInteracoesAsync(long id, long? ultimoInteracaoId = null, CancellationToken ct = default)
        {
            // Validação de autenticação
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return new JsonResult(new { success = false, message = "Sessão expirada." }) { StatusCode = 401 };
            }

            try
            {
                var interacoes = await _chamadosService.ListarInteracoesAsync(id, ct);
                
                if (interacoes == null)
                {
                    return new JsonResult(new { success = true, interacoes = new List<object>() });
                }

                // Se foi fornecido um último ID, filtrar apenas as novas
                var novasInteracoes = ultimoInteracaoId.HasValue
                    ? interacoes.Where(i => i.InteracaoId > ultimoInteracaoId.Value).ToList()
                    : interacoes.ToList();

                // Buscar também o status atualizado do chamado
                var chamado = await _chamadosService.ObterAsync(id, ct);

                return new JsonResult(new
                {
                    success = true,
                    interacoes = novasInteracoes.Select(i => new
                    {
                        interacaoId = i.InteracaoId,
                        autorNome = i.AutorNome ?? "Usuário",
                        mensagem = i.Mensagem,
                        dataCriacao = i.DataCriacao.ToString("dd/MM/yyyy HH:mm"),
                        iaGerada = i.IA_Gerada
                    }).ToList(),
                    statusId = chamado?.StatusId,
                    statusNome = chamado?.StatusNome
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar novas interações do chamado {ChamadoId}", id);
                return new JsonResult(new { success = false, message = "Erro ao buscar novas mensagens." }) { StatusCode = 500 };
            }
        }

        /// <summary>
        /// Handler POST - Adiciona uma nova interação (mensagem) ao chamado.
        /// Chamado via AJAX quando o usuário envia uma mensagem no chat.
        /// </summary>
        /// <param name="id">ID do chamado</param>
        /// <param name="mensagem">Texto da mensagem a ser enviada</param>
        /// <param name="ct">Token de cancelamento</param>
        /// <returns>Redirecionamento para a página de detalhes ou erro</returns>
        public async Task<IActionResult> OnPostAdicionarInteracaoAsync(long id, [FromForm] string mensagem, CancellationToken ct = default)
        {
            // Validação de autenticação
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return new JsonResult(new { success = false, message = "Sessão expirada. Por favor, faça login novamente." }) { StatusCode = 401 };
            }

            // Validação da mensagem
            if (string.IsNullOrWhiteSpace(mensagem))
            {
                return new JsonResult(new { success = false, message = "Mensagem não pode estar vazia." }) { StatusCode = 400 };
            }

            try
            {
                // Cria o request e envia para a API
                var request = new AdicionarInteracaoRequest(mensagem);
                var interacao = await _chamadosService.AdicionarInteracaoAsync(id, request, ct);
                
                if (interacao == null)
                {
                    _logger.LogWarning("API retornou null ao adicionar interação. ChamadoId: {ChamadoId}", id);
                    return new JsonResult(new { success = false, message = "Erro ao enviar mensagem. Resposta vazia da API." }) { StatusCode = 500 };
                }

                // Retorna JSON com a interação criada para atualizar o chat dinamicamente
                return new JsonResult(new { 
                    success = true, 
                    message = "Mensagem enviada com sucesso.",
                    interacao = new {
                        interacaoId = interacao.InteracaoId,
                        autorNome = interacao.AutorNome ?? "Usuário",
                        mensagem = interacao.Mensagem,
                        dataCriacao = interacao.DataCriacao.ToString("dd/MM/yyyy HH:mm"),
                        iaGerada = interacao.IA_Gerada
                    }
                });
            }
            catch (System.Net.Http.HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Erro HTTP ao adicionar interação ao chamado {ChamadoId}", id);
                
                var statusCode = System.Net.HttpStatusCode.InternalServerError;
                if (httpEx.Data.Contains("StatusCode") && httpEx.Data["StatusCode"] is System.Net.HttpStatusCode sc)
                {
                    statusCode = sc;
                }
                
                var apiMessage = httpEx.Data.Contains("Message") ? httpEx.Data["Message"]?.ToString() : null;
                var errorMessage = !string.IsNullOrEmpty(apiMessage) ? apiMessage : "Erro ao enviar mensagem. Tente novamente.";
                
                return new JsonResult(new { success = false, message = errorMessage }) { StatusCode = (int)statusCode };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao adicionar interação ao chamado {ChamadoId}: {Message}", id, ex.Message);
                return new JsonResult(new { success = false, message = $"Erro ao enviar mensagem: {ex.Message}" }) { StatusCode = 500 };
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
        /// Handler POST - Altera o status do chamado.
        /// Chamado via AJAX quando o usuário altera o status no ComboBox.
        /// </summary>
        /// <param name="id">ID do chamado</param>
        /// <param name="novoStatus">Novo status do chamado (byte)</param>
        /// <param name="ct">Token de cancelamento</param>
        /// <returns>JSON com resultado ou erro</returns>
        public async Task<IActionResult> OnPostAlterarStatusAsync(long id, [FromForm] byte novoStatus, CancellationToken ct = default)
        {
            try
            {
                // Verifica se a sessão está disponível
                if (HttpContext.Session == null)
                {
                    _logger.LogError("Sessão não disponível ao alterar status do chamado {ChamadoId}", id);
                    return new JsonResult(new { success = false, message = "Erro: Sessão não disponível." }) { StatusCode = 500 };
                }

                // Validação de autenticação
                var token = HttpContext.Session.GetString("Token");
                var tenantIdStr = HttpContext.Session.GetString("TenantId");
                var usuarioIdStr = HttpContext.Session.GetString("UsuarioId");
                var tipoUsuarioIdStr = HttpContext.Session.GetString("TipoUsuarioId");
                
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Tentativa de alterar status sem token. ChamadoId: {ChamadoId}", id);
                    return new JsonResult(new { success = false, message = "Sessão expirada. Por favor, faça login novamente." }) { StatusCode = 401 };
                }

                // Validação de permissão - apenas Técnicos e Administradores podem alterar status
                if (!byte.TryParse(tipoUsuarioIdStr, out var tipoUsuarioId) || (tipoUsuarioId != 2 && tipoUsuarioId != 3))
                {
                    _logger.LogWarning("Tentativa de alterar status sem permissão. UsuarioId: {UsuarioId}, TipoUsuarioId: {TipoUsuarioId}, ChamadoId: {ChamadoId}", 
                        usuarioIdStr, tipoUsuarioIdStr, id);
                    return new JsonResult(new { success = false, message = "Apenas técnicos e administradores podem alterar o status do chamado." }) { StatusCode = 403 };
                }

                // Validação do status (1-6)
                if (novoStatus < 1 || novoStatus > 6)
                {
                    _logger.LogWarning("Status inválido recebido. ChamadoId: {ChamadoId}, NovoStatus: {NovoStatus}", id, novoStatus);
                    return new JsonResult(new { success = false, message = $"Status inválido: {novoStatus}. O status deve estar entre 1 e 6." }) { StatusCode = 400 };
                }
                
                _logger.LogInformation("Iniciando alteração de status. ChamadoId: {ChamadoId}, NovoStatus: {NovoStatus}, UsuarioId: {UsuarioId}, TenantId: {TenantId}", 
                    id, novoStatus, usuarioIdStr, tenantIdStr);

                // Cria o request e envia para a API
                var request = new AlterarStatusRequest(novoStatus);
                _logger.LogInformation("Enviando requisição para API. ChamadoId: {ChamadoId}, Request: NovoStatus={NovoStatus}", id, novoStatus);
                
                var resultado = await _chamadosService.AlterarStatusAsync(id, request, ct);
                
                if (resultado == null)
                {
                    _logger.LogWarning("API retornou null ao alterar status. ChamadoId: {ChamadoId}", id);
                    return new JsonResult(new { success = false, message = "Erro ao alterar status. Resposta vazia da API." }) { StatusCode = 500 };
                }
                
                _logger.LogInformation("Status do chamado {ChamadoId} alterado com sucesso para {NovoStatus}. Novo StatusId: {StatusId}", 
                    id, novoStatus, resultado.StatusId);
                return new JsonResult(new { success = true, message = "Status alterado com sucesso." });
            }
            catch (System.Net.Http.HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Erro HTTP ao alterar status do chamado {ChamadoId}. Message: {Message}, StackTrace: {StackTrace}", 
                    id, httpEx.Message, httpEx.StackTrace);
                
                // Extrai o status code do erro
                var statusCode = System.Net.HttpStatusCode.InternalServerError;
                if (httpEx.Data.Contains("StatusCode") && httpEx.Data["StatusCode"] is System.Net.HttpStatusCode sc)
                {
                    statusCode = sc;
                }
                
                // Tenta extrair mensagem da API
                var apiMessage = httpEx.Data.Contains("Message") ? httpEx.Data["Message"]?.ToString() : null;
                var responseContent = httpEx.Data.Contains("ResponseContent") ? httpEx.Data["ResponseContent"]?.ToString() : null;
                
                // Log detalhado do erro
                _logger.LogError("Detalhes do erro HTTP - StatusCode: {StatusCode}, ApiMessage: {ApiMessage}, ResponseContent: {ResponseContent}", 
                    statusCode, apiMessage, responseContent);
                
                var errorMessage = !string.IsNullOrEmpty(apiMessage) ? apiMessage : 
                                  (!string.IsNullOrEmpty(responseContent) ? responseContent : httpEx.Message);
                
                // Retorna o status code correto
                return new JsonResult(new { success = false, message = errorMessage }) { StatusCode = (int)statusCode };
            }
            catch (System.ArgumentNullException argEx)
            {
                _logger.LogError(argEx, "Argumento nulo ao alterar status do chamado {ChamadoId}. ParamName: {ParamName}", id, argEx.ParamName);
                return new JsonResult(new { success = false, message = $"Erro: {argEx.Message}" }) { StatusCode = 400 };
            }
            catch (System.InvalidOperationException invOpEx)
            {
                _logger.LogError(invOpEx, "Operação inválida ao alterar status do chamado {ChamadoId}. Message: {Message}", id, invOpEx.Message);
                return new JsonResult(new { success = false, message = $"Erro: {invOpEx.Message}" }) { StatusCode = 500 };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao alterar status do chamado {ChamadoId}. Tipo: {Type}, Message: {Message}, StackTrace: {StackTrace}, InnerException: {InnerException}", 
                    id, ex.GetType().Name, ex.Message, ex.StackTrace, ex.InnerException?.Message);
                return new JsonResult(new { success = false, message = $"Erro ao alterar status: {ex.Message}", innerError = ex.InnerException?.Message }) { StatusCode = 500 };
            }
        }

        /// <summary>
        /// Retorna a lista de status disponíveis para o ComboBox.
        /// </summary>
        /// <returns>Lista de tuplas (valor, nome) dos status</returns>
        public List<(byte Value, string Name)> GetStatusList()
        {
            return new List<(byte, string)>
            {
                (1, "Aberto"),
                (2, "Em Andamento"),
                (3, "Pendente"),
                (4, "Resolvido"),
                (5, "Fechado"),
                (6, "Cancelado")
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

