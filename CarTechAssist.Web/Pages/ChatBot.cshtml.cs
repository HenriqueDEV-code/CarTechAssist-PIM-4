using CarTechAssist.Contracts.ChatBot;
using CarTechAssist.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarTechAssist.Web.Pages
{
    public class ChatBotModel : PageModel
    {
        private readonly ChatBotService _chatBotService;
        private readonly ChamadosService _chamadosService;
        private readonly AuthService _authService;
        private readonly ILogger<ChatBotModel> _logger;

        public string? ErrorMessage { get; set; }
        public List<ChatBotMensagemDto> Mensagens { get; set; } = new();
        public long? ChamadoAtualId { get; set; }

        [BindProperty]
        public string NovaMensagem { get; set; } = string.Empty;

        public int TenantId { get; set; }
        public int UsuarioId { get; set; }

        public ChatBotModel(
            ChatBotService chatBotService,
            ChamadosService chamadosService,
            AuthService authService,
            ILogger<ChatBotModel> logger)
        {
            _chatBotService = chatBotService;
            _chamadosService = chamadosService;
            _authService = authService;
            _logger = logger;
        }

        public Task<IActionResult> OnGetAsync(CancellationToken ct = default)
        {
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return Task.FromResult<IActionResult>(RedirectToPage("/Login"));
            }

            // Verificar se é Cliente
            var tipoUsuarioIdStr = HttpContext.Session.GetString("TipoUsuarioId");
            if (string.IsNullOrEmpty(tipoUsuarioIdStr))
            {
                _logger.LogWarning("TipoUsuarioId não encontrado na sessão. Redirecionando para login.");
                ErrorMessage = "Informações de permissão não encontradas. Por favor, faça login novamente.";
                return Task.FromResult<IActionResult>(Page());
            }
            
            if (!byte.TryParse(tipoUsuarioIdStr, out var tipoUsuarioId) || tipoUsuarioId != 1)
            {
                _logger.LogWarning("Usuário não é Cliente. TipoUsuarioId: {TipoUsuarioId}.", tipoUsuarioIdStr);
                ErrorMessage = $"Você não tem permissão para usar o ChatBot. Esta funcionalidade é apenas para clientes. (Seu perfil: TipoUsuarioId = {tipoUsuarioIdStr})";
                return Task.FromResult<IActionResult>(Page()); // Mostrar página com mensagem de erro em vez de redirecionar
            }
            
            _logger.LogInformation("Acesso ao ChatBot autorizado. TipoUsuarioId: {TipoUsuarioId}", tipoUsuarioId);

            var tenantIdStr = HttpContext.Session.GetString("TenantId");
            var usuarioIdStr = HttpContext.Session.GetString("UsuarioId");
            var nomeCompleto = HttpContext.Session.GetString("NomeCompleto");

            // Log de debug para verificar valores na sessão
            _logger.LogDebug("Valores da sessão - TenantId: {TenantId}, UsuarioId: {UsuarioId}, TipoUsuarioId: {TipoUsuarioId}, Nome: {Nome}",
                tenantIdStr, usuarioIdStr, tipoUsuarioIdStr, nomeCompleto);

            if (!int.TryParse(tenantIdStr, out var tenantId))
                tenantId = 1;
            TenantId = tenantId;

            if (!int.TryParse(usuarioIdStr, out var usuarioId))
            {
                _logger.LogWarning("UsuarioId inválido na sessão: {UsuarioIdStr}", usuarioIdStr);
                return Task.FromResult<IActionResult>(RedirectToPage("/Login"));
            }
            UsuarioId = usuarioId;

            return Task.FromResult<IActionResult>(Page());
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
        {
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            // Verificar novamente o TipoUsuarioId antes de enviar mensagem
            var tipoUsuarioIdStr = HttpContext.Session.GetString("TipoUsuarioId");
            if (!byte.TryParse(tipoUsuarioIdStr, out var tipoUsuarioId) || tipoUsuarioId != 1)
            {
                _logger.LogWarning("Tentativa de usar ChatBot sem permissão. TipoUsuarioId: {TipoUsuarioId}", tipoUsuarioIdStr);
                ErrorMessage = "Você não tem permissão para usar o ChatBot. Esta funcionalidade é apenas para clientes.";
                return Page();
            }

            var usuarioIdStr = HttpContext.Session.GetString("UsuarioId");
            if (!int.TryParse(usuarioIdStr, out var usuarioId))
            {
                return RedirectToPage("/Login");
            }

            if (string.IsNullOrWhiteSpace(NovaMensagem))
            {
                ErrorMessage = "A mensagem não pode estar vazia.";
                return await OnGetAsync(ct);
            }

            async Task<bool> TentarRefreshTokenAsync(CancellationToken tokenCt)
            {
                try
                {
                    var refreshToken = HttpContext.Session.GetString("RefreshToken");
                    if (string.IsNullOrEmpty(refreshToken)) return false;

                    var refresh = await _authService.RefreshTokenAsync(refreshToken, tokenCt);
                    if (refresh == null) return false;

                    // Atualizar sessão
                    HttpContext.Session.SetString("Token", refresh.Token);
                    HttpContext.Session.SetString("RefreshToken", refresh.RefreshToken);
                    HttpContext.Session.SetString("TipoUsuarioId", refresh.TipoUsuarioId.ToString());
                    HttpContext.Session.SetString("UsuarioId", refresh.UsuarioId.ToString());
                    HttpContext.Session.SetString("TenantId", refresh.TenantId.ToString());

                    _logger.LogInformation("Token renovado com sucesso para o ChatBot.");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Falha ao renovar token");
                    return false;
                }
            }

            try
            {
                // Validar mensagem
                var mensagemTrim = NovaMensagem.Trim();
                if (mensagemTrim.Length > 5000)
                {
                    ErrorMessage = "A mensagem é muito longa. Por favor, limite a 5000 caracteres.";
                    return Page();
                }

                _logger.LogInformation("Enviando mensagem para ChatBot. UsuarioId: {UsuarioId}, TenantId: {TenantId}, ChamadoId: {ChamadoId}, TipoUsuarioId: {TipoUsuarioId}", 
                    UsuarioId, TenantId, ChamadoAtualId, tipoUsuarioId);

                var resposta = await _chatBotService.EnviarMensagemAsync(
                    mensagemTrim,
                    ChamadoAtualId,
                    ct);

                if (resposta != null)
                {
                    _logger.LogInformation("ChatBot respondeu com sucesso. CriouChamado: {CriouChamado}, ChamadoId: {ChamadoId}", 
                        resposta.CriouChamado, resposta.ChamadoId);

                    // Se criou um chamado, atualizar e carregar histórico
                    if (resposta.CriouChamado && resposta.ChamadoId.HasValue)
                    {
                        ChamadoAtualId = resposta.ChamadoId;
                        // Aguardar um pouco para garantir que as interações foram salvas no banco
                        await Task.Delay(500, ct);
                        await CarregarHistoricoAsync(ChamadoAtualId.Value, ct);
                    }
                    else if (resposta.ChamadoId.HasValue && resposta.ChamadoId != ChamadoAtualId)
                    {
                        // Se recebeu um ChamadoId mas não criou (pode ser de contexto)
                        ChamadoAtualId = resposta.ChamadoId;
                        await CarregarHistoricoAsync(ChamadoAtualId.Value, ct);
                    }
                    else if (ChamadoAtualId.HasValue)
                    {
                        // Se já tem chamado, carregar histórico atualizado
                        await CarregarHistoricoAsync(ChamadoAtualId.Value, ct);
                    }
                    else
                    {
                        // Se não tem chamado ainda, adicionar mensagens localmente
                        Mensagens.Add(new ChatBotMensagemDto(
                            InteracaoId: 0,
                            Mensagem: mensagemTrim,
                            EhBot: false,
                            DataCriacao: DateTime.Now,
                            ChamadoId: null
                        ));

                        Mensagens.Add(new ChatBotMensagemDto(
                            InteracaoId: 0,
                            Mensagem: resposta.Resposta,
                            EhBot: true,
                            DataCriacao: DateTime.Now,
                            ChamadoId: resposta.ChamadoId
                        ));
                    }

                    NovaMensagem = string.Empty;
                    ModelState.Clear();
                }
                else
                {
                    _logger.LogWarning("ChatBot retornou resposta nula. UsuarioId: {UsuarioId}", UsuarioId);
                    ErrorMessage = "Não foi possível obter resposta do assistente. Por favor, tente novamente em alguns instantes.";
                }
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                var statusCode = ex.Data.Contains("StatusCode") ? ex.Data["StatusCode"]?.ToString() : "Desconhecido";
                var responseContent = ex.Data.Contains("ResponseContent") ? ex.Data["ResponseContent"]?.ToString() : null;
                var apiMessage = ex.Data.Contains("Message") ? ex.Data["Message"]?.ToString() : null;
                
                _logger.LogError(ex, "Erro de conexão ao enviar mensagem para ChatBot. StatusCode: {StatusCode}, ApiMessage: {ApiMessage}, ResponseContent: {ResponseContent}", 
                    statusCode, apiMessage, responseContent);
                
                // Tentativa de refresh para 401/403
                if (statusCode == "401" || statusCode == "403")
                {
                    var tipoSessao = HttpContext.Session.GetString("TipoUsuarioId");
                    if (tipoSessao == "1")
                    {
                        var renovou = await TentarRefreshTokenAsync(ct);
                        if (renovou)
                        {
                            _logger.LogInformation("Reenviando mensagem após renovar token.");
                            return await OnPostAsync(ct); // retry uma vez
                        }
                    }
                }

                if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized") || statusCode == "401")
                {
                    ErrorMessage = "Sua sessão expirou. Por favor, faça login novamente.";
                }
                else if (ex.Message.Contains("403") || ex.Message.Contains("Forbidden") || statusCode == "403")
                {
                    // Verificar o TipoUsuarioId atual na sessão para debug
                    var tipoUsuarioIdStrDebug = HttpContext.Session.GetString("TipoUsuarioId");
                    _logger.LogWarning("Acesso negado ao ChatBot. TipoUsuarioId na sessão: {TipoUsuarioId}", tipoUsuarioIdStrDebug);
                    
                    // Usar mensagem da API se disponível, senão usar mensagem padrão
                    ErrorMessage = !string.IsNullOrEmpty(apiMessage) 
                        ? apiMessage 
                        : "Você não tem permissão para usar o ChatBot. Esta funcionalidade é apenas para clientes.";
                    
                    // Se o TipoUsuarioId na sessão é 1 mas ainda está dando erro, pode ser problema no JWT
                    if (tipoUsuarioIdStrDebug == "1")
                    {
                        _logger.LogError("INCONSISTÊNCIA: TipoUsuarioId na sessão é 1 (Cliente), mas API retornou 403. Possível problema com JWT token.");
                        ErrorMessage += " Tente novamente após atualizar sua sessão.";
                    }
                }
                else if (ex.Message.Contains("500") || ex.Message.Contains("Internal Server Error"))
                {
                    ErrorMessage = "Ocorreu um erro no servidor. Nossa equipe foi notificada. Por favor, tente novamente em alguns instantes.";
                }
                else if (ex.Message.Contains("timeout") || ex.Message.Contains("Timeout"))
                {
                    ErrorMessage = "A solicitação demorou muito para responder. Verifique sua conexão e tente novamente.";
                }
                else if (ex.Message.Contains("Connection") || ex.Message.Contains("recusada"))
                {
                    ErrorMessage = "Não foi possível conectar ao servidor. Verifique sua conexão com a internet e se a API está em execução.";
                }
                else
                {
                    var mensagemApi = ex.Data.Contains("Message") ? ex.Data["Message"]?.ToString() : null;
                    ErrorMessage = !string.IsNullOrEmpty(mensagemApi) 
                        ? mensagemApi 
                        : "Erro ao comunicar com o servidor. Por favor, tente novamente.";
                }
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout ao enviar mensagem para ChatBot");
                ErrorMessage = "A solicitação demorou muito para responder. Por favor, tente novamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao enviar mensagem no ChatBot. Mensagem: {Message}, StackTrace: {StackTrace}", 
                    ex.Message, ex.StackTrace);
                ErrorMessage = "Ocorreu um erro inesperado. Nossa equipe foi notificada. Por favor, tente novamente em alguns instantes.";
            }

            return Page();
        }

        private async Task CarregarHistoricoAsync(long chamadoId, CancellationToken ct)
        {
            try
            {
                var interacoes = await _chamadosService.ListarInteracoesAsync(chamadoId, ct);
                if (interacoes != null)
                {
                    Mensagens = interacoes.Select(i => new ChatBotMensagemDto(
                        i.InteracaoId,
                        i.Mensagem ?? string.Empty,
                        i.AutorTipoUsuarioId == 4, // Bot
                        i.DataCriacao,
                        i.ChamadoId
                    )).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar histórico do ChatBot");
            }
        }
    }
}
