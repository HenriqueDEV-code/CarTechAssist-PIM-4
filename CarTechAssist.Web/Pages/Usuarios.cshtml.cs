using CarTechAssist.Contracts.Common;
using CarTechAssist.Contracts.Usuarios;
using CarTechAssist.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Linq;

namespace CarTechAssist.Web.Pages
{
    public class UsuariosModel : PageModel
    {
        private readonly UsuariosService _usuariosService;
        private readonly ILogger<UsuariosModel> _logger;

        public PagedResult<UsuarioDto>? Usuarios { get; set; }
        public string? ErrorMessage { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public byte? TipoFiltro { get; set; }

        [BindProperty]
        public CriarUsuarioRequest NovoUsuario { get; set; } = new(
            Login: string.Empty,
            NomeCompleto: string.Empty,
            Email: null,
            Telefone: null,
            TipoUsuarioId: 2, // Agente por padrão
            Senha: string.Empty
        );

        // Editar usuário
        [BindProperty]
        public int EditUsuarioId { get; set; }
        [BindProperty]
        public AtualizarUsuarioRequest EditUsuario { get; set; } = new(
            NomeCompleto: string.Empty,
            Email: null,
            Telefone: null,
            TipoUsuarioId: 2
        );

        public string? SuccessMessage { get; set; }
        public bool MostrarFormulario { get; set; }

        public UsuariosModel(UsuariosService usuariosService, ILogger<UsuariosModel> logger)
        {
            _usuariosService = usuariosService;
            _logger = logger;
        }

        public async Task<IActionResult> OnGetAsync(
            byte? tipo = null,
            int page = 1,
            string? success = null,
            CancellationToken ct = default)
        {
            _logger.LogInformation("🔍 OnGetAsync - Iniciando carregamento da página de usuários. Tipo: {Tipo}, Page: {Page}", tipo, page);
            
            // Verificar autenticação
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("❌ OnGetAsync - Token não encontrado, redirecionando para login");
                return RedirectToPage("/Login");
            }

            // Verificar se é Admin
            var tipoUsuarioIdStr = HttpContext.Session.GetString("TipoUsuarioId");
            if (!byte.TryParse(tipoUsuarioIdStr, out var tipoUsuarioId) || tipoUsuarioId != 3)
            {
                _logger.LogWarning("❌ OnGetAsync - Usuário não é admin (TipoUsuarioId: {TipoUsuarioId}), redirecionando para dashboard", tipoUsuarioIdStr);
                return RedirectToPage("/Dashboard");
            }

            CurrentPage = page;
            TipoFiltro = tipo;

            // Se há mensagem de sucesso na query string, exibir
            if (!string.IsNullOrEmpty(success))
            {
                SuccessMessage = success;
                _logger.LogInformation("✅ OnGetAsync - Mensagem de sucesso recebida: {Success}", success);
            }

            try
            {
                _logger.LogInformation("🔍 OnGetAsync - Chamando ListarAsync. Tipo: {Tipo}, Page: {Page}, PageSize: {PageSize}", tipo, page, PageSize);
                // Mostrar todos os usuários (ativos e inativos) - não filtrar por ativo
                Usuarios = await _usuariosService.ListarAsync(
                    tipo: tipo,
                    ativo: null, // null = mostrar todos (ativos e inativos)
                    page: page,
                    pageSize: PageSize,
                    ct: ct);
                
                _logger.LogInformation("✅ OnGetAsync - Usuários carregados. Total: {Total}, Items: {Count}", 
                    Usuarios?.Total ?? 0, Usuarios?.Items?.Count() ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ OnGetAsync - Erro ao carregar usuários. Tipo: {Type}, Message: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                ErrorMessage = $"Erro ao carregar usuários: {ex.Message}";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken ct = default)
        {
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            // Verificar se é Admin
            var tipoUsuarioIdStr = HttpContext.Session.GetString("TipoUsuarioId");
            if (!byte.TryParse(tipoUsuarioIdStr, out var tipoUsuarioId) || tipoUsuarioId != 3)
            {
                return RedirectToPage("/Dashboard");
            }

            _logger.LogInformation("🔍 OnPostAsync - Iniciando criação de usuário. ModelState.IsValid: {IsValid}", ModelState.IsValid);
            
            // Log dos valores recebidos diretamente do Request (antes do binding)
            if (Request.HasFormContentType)
            {
                _logger.LogInformation("🔍 OnPostAsync - Valores do Form:");
                foreach (var key in Request.Form.Keys)
                {
                    var value = Request.Form[key].ToString();
                    _logger.LogInformation("  - {Key}: '{Value}'", key, value);
                }
            }
            
            // Verificar se NovoUsuario não é null
            if (NovoUsuario == null)
            {
                _logger.LogError("❌ OnPostAsync - NovoUsuario é NULL!");
                ErrorMessage = "Erro: Dados do formulário não foram recebidos. Por favor, tente novamente.";
                MostrarFormulario = true;
                await OnGetAsync(ct: ct);
                return Page();
            }
            
            // Log dos valores recebidos (detalhado)
            _logger.LogInformation("🔍 OnPostAsync - Dados recebidos do formulário:");
            _logger.LogInformation("  - Login: '{Login}' (IsNullOrWhiteSpace: {IsEmpty})", 
                NovoUsuario.Login, string.IsNullOrWhiteSpace(NovoUsuario.Login));
            _logger.LogInformation("  - NomeCompleto: '{NomeCompleto}' (IsNullOrWhiteSpace: {IsEmpty})", 
                NovoUsuario.NomeCompleto, string.IsNullOrWhiteSpace(NovoUsuario.NomeCompleto));
            _logger.LogInformation("  - Email: '{Email}' (IsNull: {IsNull}, IsNullOrWhiteSpace: {IsEmpty})", 
                NovoUsuario.Email ?? "NULL", NovoUsuario.Email == null, string.IsNullOrWhiteSpace(NovoUsuario.Email));
            _logger.LogInformation("  - Telefone: '{Telefone}' (IsNull: {IsNull}, IsNullOrWhiteSpace: {IsEmpty})", 
                NovoUsuario.Telefone ?? "NULL", NovoUsuario.Telefone == null, string.IsNullOrWhiteSpace(NovoUsuario.Telefone));
            _logger.LogInformation("  - TipoUsuarioId: {TipoUsuarioId}", NovoUsuario.TipoUsuarioId);
            _logger.LogInformation("  - Senha: '{HasSenha}' (Length: {Length})", 
                !string.IsNullOrEmpty(NovoUsuario.Senha) ? "***" : "VAZIA", NovoUsuario.Senha?.Length ?? 0);
            
            // Ignorar erros do ModelState para records e fazer validação manual
            // O ModelState pode falhar com records, então vamos validar manualmente
            var errosValidacao = new List<string>();
            
            if (string.IsNullOrWhiteSpace(NovoUsuario.Login))
            {
                errosValidacao.Add("Login é obrigatório.");
            }
            
            if (string.IsNullOrWhiteSpace(NovoUsuario.NomeCompleto))
            {
                errosValidacao.Add("Nome Completo é obrigatório.");
            }
            
            if (string.IsNullOrWhiteSpace(NovoUsuario.Senha))
            {
                errosValidacao.Add("Senha é obrigatória.");
            }
            else if (NovoUsuario.Senha.Length < 6)
            {
                errosValidacao.Add("A senha deve ter no mínimo 6 caracteres.");
            }
            
            if (errosValidacao.Any())
            {
                _logger.LogWarning("❌ OnPostAsync - Validação manual falhou. Erros: {Errors}", string.Join(", ", errosValidacao));
                ErrorMessage = "Por favor, corrija os erros no formulário: " + string.Join(", ", errosValidacao);
                MostrarFormulario = true;
                await OnGetAsync(ct: ct);
                return Page();
            }
            
            // Limpar erros do ModelState para evitar problemas com records
            ModelState.Clear();

            try
            {
                _logger.LogInformation("🔍 OnPostAsync - Validações passaram, prosseguindo com criação");
                
                // Normalizar campos: converter strings vazias em null, mas preservar se tiver conteúdo
                // IMPORTANTE: Se o email foi informado (mesmo que vazio), preservar o valor original antes de normalizar
                string? emailNormalizado = null;
                if (!string.IsNullOrWhiteSpace(NovoUsuario.Email))
                {
                    emailNormalizado = NovoUsuario.Email.Trim();
                    // Se após trim ficar vazio, manter como null
                    if (string.IsNullOrWhiteSpace(emailNormalizado))
                    {
                        emailNormalizado = null;
                    }
                }
                
                string? telefoneNormalizado = null;
                if (!string.IsNullOrWhiteSpace(NovoUsuario.Telefone))
                {
                    telefoneNormalizado = NovoUsuario.Telefone.Trim();
                    // Se após trim ficar vazio, manter como null
                    if (string.IsNullOrWhiteSpace(telefoneNormalizado))
                    {
                        telefoneNormalizado = null;
                    }
                }

                _logger.LogInformation("🔍 OnPostAsync - Campos normalizados:");
                _logger.LogInformation("  - Email normalizado: '{Email}' (IsNull: {IsNull})", 
                    emailNormalizado ?? "NULL", emailNormalizado == null);
                _logger.LogInformation("  - Telefone normalizado: '{Telefone}' (IsNull: {IsNull})", 
                    telefoneNormalizado ?? "NULL", telefoneNormalizado == null);

                var requestNormalizado = new CriarUsuarioRequest(
                    Login: NovoUsuario.Login.Trim(),
                    NomeCompleto: NovoUsuario.NomeCompleto.Trim(),
                    Email: emailNormalizado,
                    Telefone: telefoneNormalizado,
                    TipoUsuarioId: NovoUsuario.TipoUsuarioId,
                    Senha: NovoUsuario.Senha ?? string.Empty
                );

                _logger.LogInformation("🔍 OnPostAsync - Request criado. Login: {Login}, NomeCompleto: {NomeCompleto}, Email: '{Email}', Telefone: '{Telefone}', TipoUsuarioId: {TipoUsuarioId}", 
                    requestNormalizado.Login, requestNormalizado.NomeCompleto, 
                    requestNormalizado.Email ?? "NULL", requestNormalizado.Telefone ?? "NULL", 
                    requestNormalizado.TipoUsuarioId);
                
                var resultado = await _usuariosService.CriarAsync(requestNormalizado, ct);
                
                _logger.LogInformation("🔍 OnPostAsync - Resposta da API recebida. Resultado: {Resultado}", 
                    resultado != null ? $"UsuarioId={resultado.UsuarioId}, Login={resultado.Login}" : "NULL");
                
                if (resultado != null && resultado.UsuarioId > 0)
                {
                    _logger.LogInformation("✅ Usuário criado com sucesso. UsuarioId: {UsuarioId}, Login: {Login}", 
                        resultado.UsuarioId, resultado.Login);
                    
                    // Redireciona para recarregar a página com a lista atualizada
                    // Passa a mensagem de sucesso via query string
                    return RedirectToPage("/Usuarios", new { 
                        success = $"Usuário '{resultado.NomeCompleto}' criado com sucesso!" 
                    });
                }
                else
                {
                    _logger.LogWarning("❌ API retornou null ou UsuarioId inválido ao criar usuário. Resultado: {Resultado}", 
                        resultado != null ? $"UsuarioId={resultado.UsuarioId}" : "NULL");
                    ErrorMessage = "Erro ao criar usuário. A API não retornou dados válidos.";
                    MostrarFormulario = true;
                    await OnGetAsync(ct: ct);
                    return Page();
                }
            }
            catch (System.Net.Http.HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "❌ OnPostAsync - Erro HTTP ao criar usuário. StatusCode: {StatusCode}, Message: {Message}",
                    httpEx.Data.Contains("StatusCode") ? httpEx.Data["StatusCode"] : "Desconhecido",
                    httpEx.Data.Contains("Message") ? httpEx.Data["Message"] : httpEx.Message);
                
                if (httpEx.Data.Contains("StatusCode") && httpEx.Data["StatusCode"] is System.Net.HttpStatusCode statusCode)
                {
                    if (statusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        var apiMessage = httpEx.Data.Contains("Message") ? httpEx.Data["Message"]?.ToString() : null;
                        ErrorMessage = !string.IsNullOrEmpty(apiMessage) ? apiMessage : "Dados inválidos. Verifique os campos e tente novamente.";
                    }
                    else if (statusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        ErrorMessage = "Sua sessão expirou. Por favor, faça login novamente.";
                        return RedirectToPage("/Login");
                    }
                    else
                    {
                        var apiMessage = httpEx.Data.Contains("Message") ? httpEx.Data["Message"]?.ToString() : null;
                        ErrorMessage = !string.IsNullOrEmpty(apiMessage) ? apiMessage : $"Erro ao criar usuário: {httpEx.Message}";
                    }
                }
                else
                {
                    ErrorMessage = $"Erro ao criar usuário: {httpEx.Message}";
                }
                MostrarFormulario = true;
                _logger.LogInformation("🔍 OnPostAsync - Recarregando lista após erro HTTP");
                return await OnGetAsync(ct: ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ OnPostAsync - Erro inesperado ao criar usuário. Tipo: {Type}, Message: {Message}, StackTrace: {StackTrace}, InnerException: {InnerException}",
                    ex.GetType().Name, ex.Message, ex.StackTrace, ex.InnerException?.Message);
                ErrorMessage = $"Erro ao criar usuário: {ex.Message}";
                if (ex.InnerException != null)
                {
                    ErrorMessage += $" Detalhes: {ex.InnerException.Message}";
                }
                MostrarFormulario = true;
            }

            // Sempre recarregar a lista, mesmo em caso de erro
            _logger.LogInformation("🔍 OnPostAsync - Recarregando lista de usuários após processamento");
            return await OnGetAsync(ct: ct);
        }

        public async Task<IActionResult> OnPostEditarAsync(CancellationToken ct = default)
        {
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            var tipoUsuarioIdStr = HttpContext.Session.GetString("TipoUsuarioId");
            if (!byte.TryParse(tipoUsuarioIdStr, out var tipoUsuarioId) || tipoUsuarioId != 3)
            {
                return RedirectToPage("/Dashboard");
            }

            try
            {
                var atualizado = await _usuariosService.AtualizarAsync(EditUsuarioId, EditUsuario, ct);
                if (atualizado != null)
                {
                    SuccessMessage = $"Usuário '{atualizado.NomeCompleto}' atualizado com sucesso!";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar usuário {UsuarioId}", EditUsuarioId);
                ErrorMessage = "Erro ao atualizar usuário. Verifique os dados e tente novamente.";
            }

            return await OnGetAsync(ct: ct);
        }

        public async Task<IActionResult> OnPostToggleAtivoAsync(int usuarioId, bool ativoAtual, CancellationToken ct = default)
        {
            var token = HttpContext.Session.GetString("Token");
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("❌ OnPostToggleAtivoAsync - Token não encontrado");
                return RedirectToPage("/Login");
            }

            var tipoUsuarioIdStr = HttpContext.Session.GetString("TipoUsuarioId");
            if (!byte.TryParse(tipoUsuarioIdStr, out var tipoUsuarioId) || tipoUsuarioId != 3)
            {
                _logger.LogWarning("❌ OnPostToggleAtivoAsync - Usuário não é admin (TipoUsuarioId: {TipoUsuarioId})", tipoUsuarioIdStr);
                return RedirectToPage("/Dashboard");
            }

            _logger.LogInformation("🔍 OnPostToggleAtivoAsync - Iniciando. UsuarioId: {UsuarioId}, AtivoAtual: {AtivoAtual}", 
                usuarioId, ativoAtual);

            try
            {
                var novoStatus = !ativoAtual;
                _logger.LogInformation("🔍 OnPostToggleAtivoAsync - Novo status: {NovoStatus}", novoStatus);
                
                var request = new AlterarAtivacaoRequest(novoStatus);
                var atualizado = await _usuariosService.AtivarDesativarAsync(usuarioId, request, ct);
                
                if (atualizado != null)
                {
                    _logger.LogInformation("✅ OnPostToggleAtivoAsync - Sucesso. UsuarioId: {UsuarioId}, Nome: {Nome}, Ativo: {Ativo}", 
                        atualizado.UsuarioId, atualizado.NomeCompleto, atualizado.Ativo);
                    SuccessMessage = $"Usuário '{atualizado.NomeCompleto}' {(atualizado.Ativo ? "ativado" : "desativado")} com sucesso!";
                }
                else
                {
                    _logger.LogWarning("⚠️ OnPostToggleAtivoAsync - Resposta nula da API. UsuarioId: {UsuarioId}", usuarioId);
                    ErrorMessage = "Erro ao alterar ativação. Resposta inválida da API.";
                }
            }
            catch (System.Net.Http.HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "❌ OnPostToggleAtivoAsync - Erro HTTP. UsuarioId: {UsuarioId}, Message: {Message}, StatusCode: {StatusCode}", 
                    usuarioId, httpEx.Message, httpEx.Data["StatusCode"]);
                
                // Tentar extrair mensagem mais detalhada
                var errorMessage = httpEx.Message;
                if (httpEx.Data.Contains("ResponseContent"))
                {
                    var responseContent = httpEx.Data["ResponseContent"]?.ToString();
                    if (!string.IsNullOrEmpty(responseContent))
                    {
                        try
                        {
                            var errorObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(responseContent);
                            if (errorObj.TryGetProperty("message", out var messageProp))
                            {
                                errorMessage = messageProp.GetString() ?? errorMessage;
                            }
                        }
                        catch { }
                    }
                }
                
                ErrorMessage = $"Erro ao alterar ativação: {errorMessage}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ OnPostToggleAtivoAsync - Erro inesperado. UsuarioId: {UsuarioId}, Message: {Message}, StackTrace: {StackTrace}", 
                    usuarioId, ex.Message, ex.StackTrace);
                ErrorMessage = $"Erro ao alterar ativação: {ex.Message}";
            }

            return await OnGetAsync(ct: ct);
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

        public string GetTipoUsuarioBadgeClass(byte tipoUsuarioId)
        {
            return tipoUsuarioId switch
            {
                1 => "bg-info",
                2 => "bg-primary",
                3 => "bg-danger",
                4 => "bg-secondary",
                _ => "bg-secondary"
            };
        }

        public int GetTotalPages()
        {
            if (Usuarios == null || Usuarios.PageSize <= 0)
                return 0;
            return (int)Math.Ceiling((double)Usuarios.Total / Usuarios.PageSize);
        }
    }
}

