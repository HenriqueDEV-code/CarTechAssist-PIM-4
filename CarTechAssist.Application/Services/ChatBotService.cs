using CarTechAssist.Contracts.ChatBot;
using CarTechAssist.Contracts.Tickets;
using CarTechAssist.Domain.Enums;
using CarTechAssist.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Text.RegularExpressions;
using CarTechAssist.Application.Services;

namespace CarTechAssist.Application.Services
{
    public class ChatBotService
    {
        private readonly ChamadosService _chamadosService;
        private readonly ILogger<ChatBotService> _logger;
        private readonly DialogflowService? _dialogflowService;
        private readonly IConfiguration _configuration;
        private readonly bool _usarDialogflow;

        // Cache de contexto por usuário (em produção, usar Redis ou banco de dados)
        private static readonly Dictionary<string, ChatBotContexto> _contextos = new();
        private static readonly object _lockContextos = new object();

        public ChatBotService(
            ChamadosService chamadosService,
            ILogger<ChatBotService> logger,
            IConfiguration configuration,
            IServiceProvider serviceProvider)
        {
            _chamadosService = chamadosService;
            _logger = logger;
            _configuration = configuration;
            _usarDialogflow = bool.Parse(_configuration["Dialogflow:Enabled"] ?? "false");

            if (_usarDialogflow)
            {
                try
                {
                    _dialogflowService = serviceProvider.GetService(typeof(DialogflowService)) as DialogflowService;
                    if (_dialogflowService != null && _dialogflowService.EstaHabilitado())
                    {
                        _logger.LogInformation("ChatBot usando Dialogflow para processamento de mensagens");
                    }
                    else
                    {
                        _logger.LogWarning("Dialogflow habilitado mas não configurado corretamente. Usando análise regex.");
                        _usarDialogflow = false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao inicializar Dialogflow. Usando análise regex.");
                    _usarDialogflow = false;
                }
            }
        }

        public async Task<ChatBotResponse> ProcessarMensagemAsync(
            int tenantId,
            int usuarioId,
            string mensagem,
            long? chamadoId,
            CancellationToken ct)
        {
            try
            {
                // Validações
                if (string.IsNullOrWhiteSpace(mensagem))
                {
                    _logger.LogWarning("Mensagem vazia recebida no ChatBotService. TenantId: {TenantId}, UsuarioId: {UsuarioId}", 
                        tenantId, usuarioId);
                    throw new ArgumentException("A mensagem não pode estar vazia.");
                }

                if (tenantId <= 0)
                {
                    _logger.LogWarning("TenantId inválido: {TenantId}", tenantId);
                    throw new ArgumentException("TenantId inválido.");
                }

                if (usuarioId <= 0)
                {
                    _logger.LogWarning("UsuarioId inválido: {UsuarioId}", usuarioId);
                    throw new ArgumentException("UsuarioId inválido.");
                }

                _logger.LogInformation("ChatBot recebeu mensagem. Tenant: {TenantId}, Usuario: {UsuarioId}, Chamado: {ChamadoId}, MensagemLength: {Length}", 
                    tenantId, usuarioId, chamadoId, mensagem.Length);

                // Obter ou criar contexto da conversa
                var contextoKey = $"{tenantId}_{usuarioId}_{chamadoId ?? 0}";
                var contexto = ObterContexto(contextoKey);
                
                // Adicionar mensagem ao histórico
                contexto = contexto with 
                { 
                    HistoricoMensagens = new List<string>(contexto.HistoricoMensagens) { mensagem.Trim() },
                    UltimaInteracao = DateTime.UtcNow
                };

                // Se já existe um chamado, trabalhar com ele
                if (chamadoId.HasValue)
                {
                    contexto = contexto with { ChamadoId = chamadoId.Value };
                    return await ProcessarMensagemComChamadoAsync(tenantId, usuarioId, mensagem, chamadoId.Value, contexto, ct);
                }

                // Se tem chamado no contexto mas não foi passado, usar do contexto
                if (contexto.ChamadoId.HasValue)
                {
                    return await ProcessarMensagemComChamadoAsync(tenantId, usuarioId, mensagem, contexto.ChamadoId.Value, contexto, ct);
                }

                // Analisar mensagem e determinar ação (usar Dialogflow se disponível)
                var analise = await AnalisarMensagemAsync(mensagem, contexto, tenantId, usuarioId, ct);

                // Se está aguardando confirmação, processar resposta
                if (contexto.AguardandoConfirmacao && !string.IsNullOrEmpty(contexto.AcaoPendente))
                {
                    return await ProcessarConfirmacaoAsync(tenantId, usuarioId, mensagem, contexto, ct);
                }

                // Se precisa criar chamado, pedir confirmação
                if (analise.PrecisaCriarChamado)
                {
                    return await PrepararCriacaoChamadoAsync(tenantId, usuarioId, mensagem, analise, contexto, ct);
                }

                // Responder normalmente
                var resposta = GerarRespostaInteligente(analise, contexto);
                
                // Salvar contexto atualizado
                SalvarContexto(contextoKey, contexto);
                
                return resposta;
            }
            catch (ArgumentException)
            {
                throw; // Re-throw para manter a mensagem de erro original
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao processar mensagem do ChatBot. TenantId: {TenantId}, UsuarioId: {UsuarioId}, Mensagem: {Message}", 
                    tenantId, usuarioId, ex.Message);
                throw;
            }
        }

        private ChatBotContexto ObterContexto(string key)
        {
            lock (_lockContextos)
            {
                if (_contextos.TryGetValue(key, out var contexto))
                {
                    // Limpar histórico muito antigo (mais de 1 hora)
                    if (DateTime.UtcNow - contexto.UltimaInteracao > TimeSpan.FromHours(1))
                    {
                        _contextos.Remove(key);
                        return new ChatBotContexto(null, null, new List<string>(), false, null, DateTime.UtcNow);
                    }
                    return contexto;
                }
                return new ChatBotContexto(null, null, new List<string>(), false, null, DateTime.UtcNow);
            }
        }

        private void SalvarContexto(string key, ChatBotContexto contexto)
        {
            lock (_lockContextos)
            {
                _contextos[key] = contexto;
            }
        }

        private async Task<AnaliseMensagem> AnalisarMensagemAsync(string mensagem, ChatBotContexto contexto, int tenantId, int usuarioId, CancellationToken ct)
        {
            // Tentar usar Dialogflow se estiver habilitado
            if (_usarDialogflow && _dialogflowService != null)
            {
                try
                {
                    var sessionId = $"tenant_{tenantId}_user_{usuarioId}";
                    var respostaDialogflow = await _dialogflowService.ProcessarMensagemAsync(mensagem, sessionId, ct);
                    
                    // Analisar resposta do Dialogflow e determinar se precisa criar chamado
                    var analise = new AnaliseMensagem
                    {
                        MensagemOriginal = mensagem,
                        Intencao = DetectarIntencao(respostaDialogflow.ToLowerInvariant()),
                        Urgencia = DetectarUrgencia(mensagem.ToLowerInvariant()),
                        Tema = DetectarTema(mensagem.ToLowerInvariant()),
                        PrecisaCriarChamado = PrecisaCriarChamadoPorDialogflow(respostaDialogflow, mensagem)
                    };

                    _logger.LogInformation("Dialogflow processou mensagem. Intenção: {Intencao}, PrecisaCriarChamado: {PrecisaCriarChamado}",
                        analise.Intencao, analise.PrecisaCriarChamado);

                    return analise;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao processar com Dialogflow. Usando análise regex.");
                    // Fallback para regex
                }
            }

            // Fallback para análise regex
            return AnalisarMensagemAvancada(mensagem, contexto);
        }

        private bool PrecisaCriarChamadoPorDialogflow(string respostaDialogflow, string mensagemOriginal)
        {
            // Palavras-chave que indicam necessidade de criar chamado
            var palavrasProblema = new[] { "problema", "erro", "bug", "não funciona", "quebrado", "ajuda", "suporte" };
            return palavrasProblema.Any(p => mensagemOriginal.ToLowerInvariant().Contains(p));
        }

        private AnaliseMensagem AnalisarMensagemAvancada(string mensagem, ChatBotContexto contexto)
        {
            var mensagemLower = mensagem.Trim().ToLowerInvariant();
            var analise = new AnaliseMensagem
            {
                Intencao = DetectarIntencao(mensagemLower),
                Urgencia = DetectarUrgencia(mensagemLower),
                Tema = DetectarTema(mensagemLower),
                PrecisaCriarChamado = false,
                MensagemOriginal = mensagem
            };

            // Padrões que indicam necessidade de criar chamado
            var padroesProblema = new[]
            {
                @"não funciona", @"não está funcionando", @"erro", @"bug", @"problema",
                @"quebrado", @"travado", @"lento", @"falha", @"defeito",
                @"preciso de ajuda", @"preciso de suporte", @"não consigo",
                @"como faço para", @"não sei como", @"ajuda com",
                @"urgente", @"emergência", @"crítico"
            };

            foreach (var padrao in padroesProblema)
            {
                if (Regex.IsMatch(mensagemLower, padrao, RegexOptions.IgnoreCase))
                {
                    analise.PrecisaCriarChamado = true;
                    break;
                }
            }

            // Se é saudação simples, não precisa de chamado
            if (Regex.IsMatch(mensagemLower, @"^(oi|olá|ola|bom dia|boa tarde|boa noite|hello|hi|hey)$", RegexOptions.IgnoreCase))
            {
                analise.PrecisaCriarChamado = false;
                analise.Intencao = "saudacao";
            }

            return analise;
        }

        private string DetectarIntencao(string mensagem)
        {
            if (Regex.IsMatch(mensagem, @"^(oi|olá|ola|bom dia|boa tarde|boa noite|hello|hi)$"))
                return "saudacao";
            
            if (Regex.IsMatch(mensagem, @"(obrigado|obrigada|thanks|valeu|agradeço)"))
                return "agradecimento";
            
            if (Regex.IsMatch(mensagem, @"(tchau|até logo|bye|encerrar|fechar|sair)"))
                return "despedida";
            
            if (Regex.IsMatch(mensagem, @"(status|andamento|como está|quando)"))
                return "consulta";
            
            if (Regex.IsMatch(mensagem, @"(problema|erro|bug|não funciona|quebrado)"))
                return "reportar_problema";
            
            if (Regex.IsMatch(mensagem, @"(ajuda|help|como|instrução|tutorial)"))
                return "solicitar_ajuda";
            
            return "geral";
        }

        private byte DetectarUrgencia(string mensagem)
        {
            if (Regex.IsMatch(mensagem, @"(urgente|emergência|emergencia|crítico|critico|imediato|agora)"))
                return 4; // Crítica
            
            if (Regex.IsMatch(mensagem, @"(importante|rápido|rapido|logo)"))
                return 3; // Alta
            
            return 2; // Média (padrão)
        }

        private string DetectarTema(string mensagem)
        {
            // Foco em suporte técnico: sistema, rede, logs
            if (Regex.IsMatch(mensagem, @"(login|senha|acesso|entrar|logar|autenticação|autenticacao|credenciais)"))
                return "autenticacao";
            
            if (Regex.IsMatch(mensagem, @"(rede|conexão|conexao|wi-fi|wifi|internet|dns|ip|ping|conectividade)"))
                return "rede";
            
            if (Regex.IsMatch(mensagem, @"(log|logs|registro|registros|erro|exception|trace|debug|auditoria)"))
                return "logs";
            
            if (Regex.IsMatch(mensagem, @"(sistema|lento|carregando|travado|bug|crash|erro 500|timeout|performance)"))
                return "sistema";
            
            if (Regex.IsMatch(mensagem, @"(chamado|ticket|protocolo|solicitacao)"))
                return "chamados";
            
            // Se não for suporte técnico, manter foco
            return "geral";
        }

        private ChatBotResponse GerarRespostaInteligente(AnaliseMensagem analise, ChatBotContexto contexto)
        {
            // Gerar sugestões de soluções antes de criar chamado
            var sugestoes = GerarSugestoesPorTema(analise.Tema, analise.MensagemOriginal);
            
            var resposta = analise.Intencao switch
            {
                "saudacao" => "Olá! 👋 Sou seu assistente técnico do CarTechAssist. " +
                             "Estou aqui para ajudar com questões de **sistema, rede e logs**. Como posso ajudar?",
                
                "agradecimento" => "De nada! 😊 Fico feliz em ajudar. Se precisar de mais alguma coisa, estarei aqui!",
                
                "despedida" => "Até logo! 👋 Foi um prazer ajudá-lo. Volte sempre que precisar!",
                
                "consulta" => "Para consultar seus chamados, acesse o menu 'Chamados' no sistema. " +
                             "Lá você verá todos os seus tickets e poderá acompanhar o status de cada um.",
                
                "solicitar_ajuda" => "Claro! Posso ajudar com questões técnicas:\n\n" +
                                    "• Problemas de **sistema** (lentidão, travamentos, bugs)\n" +
                                    "• Problemas de **rede** (conexão, DNS, IP)\n" +
                                    "• Análise de **logs** (erros, auditoria)\n" +
                                    "• Autenticação e acesso\n\n" +
                                    "Descreva sua necessidade técnica e eu tentarei diagnosticar ou criar um chamado.",
                
                _ => sugestoes.Count > 0 
                    ? $"Diagnostiquei um problema relacionado a **{analise.Tema}**. Antes de criar um chamado, que tal tentar estas soluções?\n\n{sugestoes}\n\n" +
                      "Se essas soluções não resolverem, posso criar um chamado para nossa equipe técnica. Deseja criar agora?"
                    : "Entendi sua solicitação técnica. Para melhor atendê-lo, posso criar um chamado para nossa equipe analisar. Deseja que eu crie um chamado agora?"
            };

            return new ChatBotResponse(Resposta: resposta, Sugestoes: sugestoes);
        }

        private List<string> GerarSugestoesPorTema(string tema, string mensagem)
        {
            var sugestoes = new List<string>();

            switch (tema)
            {
                case "autenticacao":
                    sugestoes.Add("• Verifique se você está usando as credenciais corretas");
                    sugestoes.Add("• Tente limpar o cache do navegador");
                    sugestoes.Add("• Verifique se a senha não expirou (use a recuperação de senha se necessário)");
                    break;

                case "rede":
                    sugestoes.Add("• Verifique sua conexão com a internet");
                    sugestoes.Add("• Teste o ping para o servidor: `ping servidor.exemplo.com`");
                    sugestoes.Add("• Verifique se o firewall não está bloqueando a conexão");
                    sugestoes.Add("• Tente desconectar e reconectar à rede");
                    break;

                case "logs":
                    sugestoes.Add("• Verifique os logs mais recentes no sistema");
                    sugestoes.Add("• Procure por erros ou exceções nas últimas 24 horas");
                    sugestoes.Add("• Verifique se há padrões recorrentes nos registros");
                    break;

                case "sistema":
                    sugestoes.Add("• Tente atualizar a página (F5 ou Ctrl+R)");
                    sugestoes.Add("• Limpe o cache do navegador");
                    sugestoes.Add("• Verifique se há atualizações pendentes do sistema");
                    sugestoes.Add("• Se for erro de timeout, tente novamente em alguns instantes");
                    break;

                default:
                    if (Regex.IsMatch(mensagem.ToLowerInvariant(), @"(lento|travado|carregando)"))
                    {
                        sugestoes.Add("• Limpe o cache do navegador");
                        sugestoes.Add("• Feche outras abas/processos que possam estar consumindo recursos");
                        sugestoes.Add("• Verifique sua conexão com a internet");
                    }
                    break;
            }

            return sugestoes;
        }

        private Task<ChatBotResponse> PrepararCriacaoChamadoAsync(
            int tenantId,
            int usuarioId,
            string mensagem,
            AnaliseMensagem analise,
            ChatBotContexto contexto,
            CancellationToken ct)
        {
            // Pedir confirmação antes de criar
            var respostaConfirmacao = $"Compreendi seu problema relacionado a **{analise.Tema}**. " +
                                    $"Para que nossa equipe técnica possa ajudar, preciso criar um chamado no sistema.\n\n" +
                                    $"**Resumo do problema:**\n{mensagem}\n\n" +
                                    $"Você deseja que eu crie este chamado agora? (Responda 'sim' ou 'não')";

            var novoContexto = contexto with
            {
                AguardandoConfirmacao = true,
                AcaoPendente = "criar_chamado",
                TemaConversa = analise.Tema
            };

            SalvarContexto($"{tenantId}_{usuarioId}_0", novoContexto);

            return Task.FromResult(new ChatBotResponse(
                Resposta: respostaConfirmacao,
                AguardandoConfirmacao: true,
                TipoConfirmacao: "criar_chamado",
                Contexto: new Dictionary<string, string>
                {
                    { "mensagem_original", mensagem },
                    { "tema", analise.Tema },
                    { "urgencia", analise.Urgencia.ToString() }
                }
            ));
        }

        private async Task<ChatBotResponse> ProcessarConfirmacaoAsync(
            int tenantId,
            int usuarioId,
            string mensagem,
            ChatBotContexto contexto,
            CancellationToken ct)
        {
            var mensagemLower = mensagem.Trim().ToLowerInvariant();
            var confirmou = mensagemLower == "sim" || mensagemLower == "s" || mensagemLower == "yes" || 
                           mensagemLower == "ok" || mensagemLower == "confirmo" || mensagemLower.Contains("confirm");

            if (!confirmou && mensagemLower != "não" && mensagemLower != "nao" && mensagemLower != "no")
            {
            return new ChatBotResponse(
                Resposta: "Não entendi sua resposta. Por favor, responda 'sim' para criar o chamado ou 'não' para cancelar.",
                AguardandoConfirmacao: true
            );
            }

            if (!confirmou)
            {
                SalvarContexto($"{tenantId}_{usuarioId}_0", contexto with { AguardandoConfirmacao = false, AcaoPendente = null });
                return new ChatBotResponse(
                    Resposta: "Entendido! Se mudar de ideia, é só me avisar e posso criar o chamado. Estou aqui para ajudar! 😊"
                );
            }

            // Criar chamado
            if (contexto.AcaoPendente == "criar_chamado")
            {
                var mensagemOriginal = contexto.HistoricoMensagens.LastOrDefault() ?? mensagem;
                
                var chamado = await CriarChamadoInteligenteAsync(tenantId, usuarioId, mensagemOriginal, contexto, ct);
                
                if (chamado != null)
                {
                    // Adicionar mensagem inicial do bot no chamado
                    try
                    {
                        await _chamadosService.AdicionarInteracaoIaAsync(
                            chamado.ChamadoId,
                            tenantId,
                            "ChatBot",
                            $"Olá! Criei este chamado com base na sua solicitação. Um técnico entrará em contato em breve. Chamado #{chamado.Numero}",
                            null,
                            "Chamado criado automaticamente pelo ChatBot",
                            "CarTechAssist-ChatBot",
                            null,
                            null,
                            null,
                            ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Não foi possível adicionar mensagem inicial do bot ao chamado {ChamadoId}", chamado.ChamadoId);
                    }

                    SalvarContexto($"{tenantId}_{usuarioId}_0", new ChatBotContexto(
                        chamado.ChamadoId,
                        contexto.TemaConversa,
                        contexto.HistoricoMensagens,
                        false,
                        null,
                        DateTime.UtcNow
                    ));

                    return new ChatBotResponse(
                        Resposta: $"✅ **Chamado criado com sucesso!**\n\n" +
                                 $"📋 **Número do chamado:** #{chamado.Numero}\n" +
                                 $"📝 **Título:** {chamado.Titulo}\n\n" +
                                 $"Nossa equipe técnica foi notificada e entrará em contato em breve. " +
                                 $"Você pode acompanhar o andamento deste chamado na seção 'Chamados' do sistema.\n\n" +
                                 $"**Resumo da conversa foi registrado para ajudar nosso técnico.**\n\n" +
                                 $"Se precisar adicionar mais informações ou fazer alguma pergunta, é só me avisar!",
                        CriouChamado: true,
                        ChamadoId: chamado.ChamadoId,
                        PrecisaEscalarParaHumano: true,
                        SugestaoAcao: "Acompanhar chamado",
                        AguardandoConfirmacao: false
                    );
                }
            }

            SalvarContexto($"{tenantId}_{usuarioId}_0", contexto with { AguardandoConfirmacao = false, AcaoPendente = null });

            return new ChatBotResponse(
                Resposta: "Ocorreu um erro ao criar o chamado. Por favor, tente novamente ou entre em contato com o suporte.",
                PrecisaEscalarParaHumano: true
            );
        }

        private async Task<ChamadoDetailDto?> CriarChamadoInteligenteAsync(
            int tenantId,
            int usuarioId,
            string descricao,
            ChatBotContexto contexto,
            CancellationToken ct)
        {
            try
            {
                // Gerar resumo da conversa do histórico
                var resumoConversa = contexto.HistoricoMensagens.Any()
                    ? $"**Histórico da conversa com o ChatBot:**\n\n" + 
                      string.Join("\n", contexto.HistoricoMensagens.Select((msg, idx) => $"{idx + 1}. {msg}")) + 
                      $"\n\n**Problema identificado:** {descricao}"
                    : descricao;
                // Gerar título inteligente baseado na descrição
                var titulo = GerarTituloChamado(descricao, contexto.TemaConversa);
                
                // Determinar prioridade baseada no contexto
                byte prioridadeId = 2; // Média por padrão
                if (descricao.ToLower().Contains("urgente") || descricao.ToLower().Contains("crítico"))
                    prioridadeId = 4; // Crítica
                else if (descricao.ToLower().Contains("importante"))
                    prioridadeId = 3; // Alta

                var request = new CriarChamadoRequest(
                    Titulo: titulo,
                    Descricao: resumoConversa,
                    CategoriaId: null,
                    PrioridadeId: prioridadeId,
                    CanalId: 4, // Chatbot
                    SolicitanteUsuarioId: usuarioId,
                    ResponsavelUsuarioId: null // Será atribuído automaticamente
                );

                _logger.LogInformation("Criando chamado inteligente. TenantId: {TenantId}, UsuarioId: {UsuarioId}, Tema: {Tema}",
                    tenantId, usuarioId, contexto.TemaConversa);

                return await _chamadosService.CriarAsync(tenantId, request, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar chamado inteligente");
                return null;
            }
        }

        private string GerarTituloChamado(string descricao, string? tema)
        {
            // Limitar descrição para título
            var palavras = descricao.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var titulo = string.Join(" ", palavras.Take(10));
            
            if (titulo.Length > 60)
                titulo = titulo.Substring(0, 57) + "...";
            
            // Adicionar prefixo baseado no tema se disponível
            if (!string.IsNullOrEmpty(tema) && tema != "geral")
            {
                var temaFormatado = tema switch
                {
                    "autenticacao" => "Acesso",
                    "chamados" => "Chamados",
                    "sistema" => "Sistema",
                    "dados" => "Dados",
                    _ => "Suporte"
                };
                return $"[{temaFormatado}] {titulo}";
            }
            
            return $"ChatBot: {titulo}";
        }

        private async Task<ChatBotResponse> ProcessarMensagemComChamadoAsync(
            int tenantId,
            int usuarioId,
            string mensagem,
            long chamadoId,
            ChatBotContexto contexto,
            CancellationToken ct)
        {
            // Adicionar mensagem do usuário ao chamado
            try
            {
                await _chamadosService.AdicionarInteracaoAsync(
                    tenantId,
                    chamadoId,
                    usuarioId,
                    new AdicionarInteracaoRequest(Mensagem: mensagem),
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao adicionar mensagem do usuário ao chamado {ChamadoId}", chamadoId);
            }

            // Analisar mensagem e responder (com Dialogflow se disponível)
            var analise = await AnalisarMensagemAsync(mensagem, contexto, tenantId, usuarioId, ct);
            var respostaBot = GerarRespostaParaChamadoExistente(analise, chamadoId);

            // Adicionar resposta do bot ao chamado
            try
            {
                await _chamadosService.AdicionarInteracaoIaAsync(
                    chamadoId,
                    tenantId,
                    "ChatBot",
                    respostaBot.Resposta,
                    null,
                    "Resposta automática do ChatBot",
                    "CarTechAssist-ChatBot",
                    null,
                    null,
                    null,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Não foi possível adicionar resposta do bot como interação IA ao chamado {ChamadoId}", chamadoId);
            }

            SalvarContexto($"{tenantId}_{usuarioId}_{chamadoId}", contexto with { ChamadoId = chamadoId });

            return respostaBot;
        }

        private ChatBotResponse GerarRespostaParaChamadoExistente(AnaliseMensagem analise, long chamadoId)
        {
            var resposta = analise.Intencao switch
            {
                "consulta" => $"Este é o chamado #{chamadoId}. Você pode acompanhar o status na seção 'Chamados' do sistema. " +
                             $"Nossa equipe está trabalhando para resolver sua solicitação.",
                
                "agradecimento" => "Obrigado pela sua paciência! Estamos trabalhando para resolver seu chamado o mais rápido possível.",
                
                _ => "Recebi sua mensagem! Ela foi adicionada ao chamado #{chamadoId} e nossa equipe técnica foi notificada. " +
                     $"Continue me informando sobre o problema e eu registrarei tudo no chamado."
            };

            return new ChatBotResponse(
                Resposta: resposta,
                ChamadoId: chamadoId
            );
        }

        // Classe auxiliar para análise de mensagem
        private class AnaliseMensagem
        {
            public string Intencao { get; set; } = "geral";
            public byte Urgencia { get; set; } = 2;
            public string Tema { get; set; } = "geral";
            public bool PrecisaCriarChamado { get; set; }
            public string MensagemOriginal { get; set; } = string.Empty;
        }
    }
}
