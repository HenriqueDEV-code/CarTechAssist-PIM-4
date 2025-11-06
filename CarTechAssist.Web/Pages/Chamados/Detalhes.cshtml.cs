using CarTechAssist.Contracts.Tickets;
using CarTechAssist.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarTechAssist.Web.Pages.Chamados
{
    public class DetalhesModel : PageModel
    {
        private readonly ChamadosService _chamadosService;
        private readonly ILogger<DetalhesModel> _logger;

        public ChamadoDetailDto? Chamado { get; set; }
        public IReadOnlyList<InteracaoDto>? Interacoes { get; set; }
        public IReadOnlyList<AnexoDto>? Anexos { get; set; }
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        [BindProperty]
        public string NovaMensagem { get; set; } = string.Empty;

        [BindProperty]
        public IFormFile? ArquivoUpload { get; set; }

        [BindProperty]
        public byte? NovoStatus { get; set; }

        public int TenantId { get; set; }
        public int UsuarioId { get; set; }
        public byte TipoUsuarioId { get; set; }

        public DetalhesModel(
            ChamadosService chamadosService,
            ILogger<DetalhesModel> logger)
        {
            _chamadosService = chamadosService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync(long id, CancellationToken ct = default)
        {
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            var tenantIdStr = HttpContext.Session.GetString("TenantId");
            var usuarioIdStr = HttpContext.Session.GetString("UsuarioId");
            var tipoUsuarioIdStr = HttpContext.Session.GetString("TipoUsuarioId");

            if (!int.TryParse(tenantIdStr, out var tenantId))
                tenantId = 1;
            TenantId = tenantId;

            if (!int.TryParse(usuarioIdStr, out var usuarioId))
            {
                return RedirectToPage("/Login");
            }
            UsuarioId = usuarioId;

            if (!byte.TryParse(tipoUsuarioIdStr, out var tipoUsuarioId))
                tipoUsuarioId = 1;
            TipoUsuarioId = tipoUsuarioId;

            try
            {
                _logger.LogInformation("Carregando detalhes do chamado {ChamadoId}. TenantId: {TenantId}, UsuarioId: {UsuarioId}, TipoUsuarioId: {TipoUsuarioId}",
                    id, TenantId, UsuarioId, tipoUsuarioId);

                Chamado = await _chamadosService.ObterAsync(id, ct);
                if (Chamado == null)
                {
                    _logger.LogWarning("Chamado {ChamadoId} não encontrado", id);
                    ErrorMessage = "Chamado não encontrado.";
                    return RedirectToPage("/Chamados");
                }

                // Verificar se o usuário tem permissão para ver este chamado
                if (tipoUsuarioId == 1 && Chamado.SolicitanteUsuarioId != usuarioId)
                {
                    _logger.LogWarning("Usuário {UsuarioId} tentou acessar chamado {ChamadoId} sem permissão", usuarioId, id);
                    ErrorMessage = "Você não tem permissão para ver este chamado.";
                    return RedirectToPage("/Chamados");
                }

                // CORREÇÃO: Se for Técnico (2) ou Admin (3) e o chamado estiver "Aberto" (1), alterar automaticamente para "Em Andamento" (2)
                // IMPORTANTE: Só fazer isso na primeira vez que o técnico acessa, não em recarregamentos
                // Verificar se não é um recarregamento após alteração de status ou envio de mensagem (verificar query string)
                var isReloadAfterStatusChange = Request.Query.ContainsKey("statusChanged");
                var isReloadAfterMessage = Request.Query.ContainsKey("messageSent");
                
                if ((tipoUsuarioId == 2 || tipoUsuarioId == 3) && Chamado.StatusId == 1 && !isReloadAfterStatusChange && !isReloadAfterMessage)
                {
                    try
                    {
                        _logger.LogInformation("Técnico/Admin {UsuarioId} acessou chamado {ChamadoId} com status Aberto. Alterando automaticamente para Em Andamento.",
                            usuarioId, id);

                        var alterarStatusRequest = new AlterarStatusRequest(NovoStatus: 2); // 2 = Em Andamento
                        var resultado = await _chamadosService.AlterarStatusAsync(id, alterarStatusRequest, ct);
                        
                        if (resultado != null)
                        {
                            // Atualizar o objeto Chamado com o novo status
                            Chamado = resultado;
                            _logger.LogInformation("Status do chamado {ChamadoId} alterado automaticamente de Aberto para Em Andamento pelo usuário {UsuarioId}",
                                id, usuarioId);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log do erro mas não bloquear o acesso ao chamado
                        _logger.LogWarning(ex, "Erro ao alterar status automaticamente do chamado {ChamadoId} para Em Andamento. Continuando com o status atual.",
                            id);
                    }
                }

                // Carregar interações (mensagens)
                Interacoes = await _chamadosService.ListarInteracoesAsync(id, ct);
                
                // Carregar anexos
                Anexos = await _chamadosService.ListarAnexosAsync(id, ct);

                _logger.LogInformation("Detalhes do chamado {ChamadoId} carregados com sucesso. Interações: {InteracoesCount}, Anexos: {AnexosCount}",
                    id, Interacoes?.Count ?? 0, Anexos?.Count ?? 0);
            }
            catch (System.Net.Http.HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Erro HTTP ao carregar detalhes do chamado {ChamadoId}. StatusCode: {StatusCode}, Message: {Message}",
                    id,
                    httpEx.Data.Contains("StatusCode") ? httpEx.Data["StatusCode"] : "Desconhecido",
                    httpEx.Data.Contains("Message") ? httpEx.Data["Message"] : httpEx.Message);
                
                // Verificar se é erro de autenticação
                if (httpEx.Data.Contains("StatusCode") && httpEx.Data["StatusCode"] is System.Net.HttpStatusCode statusCode)
                {
                    if (statusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        ErrorMessage = "Sua sessão expirou. Por favor, faça login novamente.";
                        return RedirectToPage("/Login");
                    }
                    else if (statusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        ErrorMessage = "Chamado não encontrado.";
                        return RedirectToPage("/Chamados");
                    }
                    else if (statusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        ErrorMessage = "Você não tem permissão para ver este chamado.";
                        return RedirectToPage("/Chamados");
                    }
                    else
                    {
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
                _logger.LogError(ex, "Erro inesperado ao carregar detalhes do chamado {ChamadoId}. Tipo: {Type}, Mensagem: {Message}, StackTrace: {StackTrace}",
                    id, ex.GetType().Name, ex.Message, ex.StackTrace);
                ErrorMessage = "Erro ao carregar chamado.";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(long id, CancellationToken ct = default)
        {
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            var usuarioIdStr = HttpContext.Session.GetString("UsuarioId");
            if (!int.TryParse(usuarioIdStr, out var usuarioId))
            {
                return RedirectToPage("/Login");
            }

            if (string.IsNullOrWhiteSpace(NovaMensagem))
            {
                ErrorMessage = "A mensagem não pode estar vazia.";
                return await OnGetAsync(id, ct);
            }

            try
            {
                var request = new AdicionarInteracaoRequest(Mensagem: NovaMensagem.Trim());
                var resultado = await _chamadosService.AdicionarInteracaoAsync(id, request, ct);
                
                if (resultado != null)
                {
                    SuccessMessage = "Mensagem enviada com sucesso!";
                    NovaMensagem = string.Empty;
                    // Aguardar um pouco para garantir que a mensagem foi salva no banco
                    await Task.Delay(500, ct);
                    // Recarregar página para mostrar nova mensagem, mas sem alterar status automaticamente
                    return RedirectToPage("/Chamados/Detalhes", new { id = id, messageSent = true });
                }
                else
                {
                    ErrorMessage = "Erro ao enviar mensagem.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar mensagem no chamado {ChamadoId}", id);
                ErrorMessage = "Erro ao enviar mensagem. Tente novamente.";
            }

            return await OnGetAsync(id, ct);
        }

        public async Task<IActionResult> OnPostUploadAsync(long id, CancellationToken ct = default)
        {
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            if (ArquivoUpload == null || ArquivoUpload.Length == 0)
            {
                ErrorMessage = "Nenhum arquivo selecionado.";
                return await OnGetAsync(id, ct);
            }

            try
            {
                await _chamadosService.UploadAnexoAsync(id, ArquivoUpload, ct);
                SuccessMessage = "Arquivo enviado com sucesso!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao fazer upload de anexo no chamado {ChamadoId}", id);
                ErrorMessage = "Erro ao enviar arquivo. Verifique o tamanho e tipo do arquivo.";
            }

            return await OnGetAsync(id, ct);
        }

        public async Task<IActionResult> OnPostAlterarStatusAsync(long id, CancellationToken ct = default)
        {
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            var usuarioIdStr = HttpContext.Session.GetString("UsuarioId");
            if (!int.TryParse(usuarioIdStr, out var usuarioId))
            {
                return RedirectToPage("/Login");
            }

            var tipoUsuarioIdStr = HttpContext.Session.GetString("TipoUsuarioId");
            if (!byte.TryParse(tipoUsuarioIdStr, out var tipoUsuarioId) || (tipoUsuarioId != 2 && tipoUsuarioId != 3))
            {
                ErrorMessage = "Apenas técnicos podem alterar o status do chamado.";
                return await OnGetAsync(id, ct);
            }

            if (!NovoStatus.HasValue)
            {
                ErrorMessage = "Selecione um status válido.";
                return await OnGetAsync(id, ct);
            }

            try
            {
                var request = new AlterarStatusRequest(NovoStatus: NovoStatus.Value);
                var resultado = await _chamadosService.AlterarStatusAsync(id, request, ct);
                
                if (resultado != null)
                {
                    SuccessMessage = "Status atualizado com sucesso!";
                    // Aguardar um pouco para garantir que o status foi salvo no banco antes de recarregar
                    await Task.Delay(500, ct);
                    // Recarregar página para mostrar novo status, adicionando flag para evitar alteração automática
                    return RedirectToPage("/Chamados/Detalhes", new { id = id, statusChanged = true });
                }
                else
                {
                    ErrorMessage = "Erro ao atualizar status.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao alterar status do chamado {ChamadoId}", id);
                ErrorMessage = "Erro ao atualizar status. Tente novamente.";
            }

            return await OnGetAsync(id, ct);
        }

        public string GetTipoUsuarioNome(byte tipoUsuarioId)
        {
            return tipoUsuarioId switch
            {
                1 => "Cliente",
                2 => "Agente",
                3 => "Admin",
                4 => "Bot",
                _ => "Desconhecido"
            };
        }

        public string GetStatusNome(byte statusId)
        {
            return statusId switch
            {
                1 => "Aberto",
                2 => "Em Andamento",
                3 => "Aguardando Usuário",
                4 => "Resolvido",
                5 => "Fechado",
                6 => "Cancelado",
                _ => "Desconhecido"
            };
        }

        public bool IsTecnico()
        {
            // Técnico = TipoUsuarioId 2 (Agente) ou 3 (Admin)
            return TipoUsuarioId == 2 || TipoUsuarioId == 3;
        }

        public string GetPrioridadeNome(byte prioridadeId)
        {
            return prioridadeId switch
            {
                1 => "Baixa",
                2 => "Média",
                3 => "Alta",
                4 => "Urgente",
                _ => "Desconhecida"
            };
        }

        public string FormatarTamanho(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}

