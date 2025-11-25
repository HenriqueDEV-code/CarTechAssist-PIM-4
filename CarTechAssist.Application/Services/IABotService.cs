using CarTechAssist.Domain.Entities;
using CarTechAssist.Domain.Enums;
using CarTechAssist.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace CarTechAssist.Application.Services
{
    /// <summary>
    /// Serviço responsável por processar chamados de clientes usando IA.
    /// Analisa o chamado, conversa com o cliente, tenta resolver e atualiza status.
    /// </summary>
    public class IABotService
    {
        private readonly IAiProvider _aiProvider;
        private readonly IChamadosRepository _chamadosRepository;
        private readonly IUsuariosRepository _usuariosRepository;
        private readonly ILogger<IABotService> _logger;
        private readonly ChamadosService _chamadosService;
        private readonly IIARunLogRepository? _iaRunLogRepository;

        public IABotService(
            IAiProvider aiProvider,
            IChamadosRepository chamadosRepository,
            IUsuariosRepository usuariosRepository,
            ILogger<IABotService> logger,
            ChamadosService chamadosService,
            IIARunLogRepository? iaRunLogRepository = null)
        {
            _aiProvider = aiProvider;
            _chamadosRepository = chamadosRepository;
            _usuariosRepository = usuariosRepository;
            _logger = logger;
            _chamadosService = chamadosService;
            _iaRunLogRepository = iaRunLogRepository;
        }

        /// <summary>
        /// Processa um chamado criado por um cliente, analisando e respondendo automaticamente.
        /// </summary>
        public async Task<ProcessarChamadoResult> ProcessarChamadoAsync(
            long chamadoId,
            int tenantId,
            CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("🤖 Iniciando processamento do chamado {ChamadoId} pelo Bot IA", chamadoId);

                // 1. Buscar o chamado
                var chamado = await _chamadosRepository.ObterAsync(chamadoId, ct);
                if (chamado == null)
                {
                    throw new InvalidOperationException($"Chamado {chamadoId} não encontrado.");
                }

                if (chamado.TenantId != tenantId)
                {
                    throw new UnauthorizedAccessException($"Chamado {chamadoId} não pertence ao tenant {tenantId}.");
                }

                // Verificar se o chamado foi criado por um cliente
                var solicitante = await _usuariosRepository.ObterPorIdAsync(chamado.SolicitanteUsuarioId, ct);
                if (solicitante == null)
                {
                    _logger.LogWarning("⚠️ Solicitante {SolicitanteUsuarioId} não encontrado para o chamado {ChamadoId}. Pulando processamento IA.", chamado.SolicitanteUsuarioId, chamadoId);
                    return new ProcessarChamadoResult
                    {
                        Sucesso = false,
                        Mensagem = "Solicitante do chamado não encontrado.",
                        StatusAtualizado = false
                    };
                }
                
                _logger.LogInformation("🔍 Solicitante encontrado: UsuarioId={UsuarioId}, TipoUsuarioId={TipoUsuarioId}, Nome={Nome}", 
                    solicitante.UsuarioId, solicitante.TipoUsuarioId, solicitante.NomeCompleto);
                
                if ((byte)solicitante.TipoUsuarioId != (byte)TipoUsuarios.Cliente)
                {
                    _logger.LogInformation("ℹ️ Chamado {ChamadoId} não foi criado por um cliente (TipoUsuarioId={TipoUsuarioId}). Pulando processamento IA.", 
                        chamadoId, solicitante.TipoUsuarioId);
                    return new ProcessarChamadoResult
                    {
                        Sucesso = false,
                        Mensagem = "Chamado não foi criado por um cliente.",
                        StatusAtualizado = false
                    };
                }
                
                _logger.LogInformation("✅ Chamado {ChamadoId} foi criado por um cliente. Prosseguindo com processamento IA.", chamadoId);

                // 1.5. Verificar se o chamado pode ser processado pela IA
                if (!PodeProcessarChamado(chamado.StatusId))
                {
                    var statusNome = EnumHelperService.GetStatusNome(chamado.StatusId);
                    _logger.LogInformation("⏸️ Chamado {ChamadoId} com status {Status} não pode ser processado pela IA. Apenas agente humano pode atender.", chamadoId, statusNome);
                    return new ProcessarChamadoResult
                    {
                        Sucesso = false,
                        Mensagem = $"Este chamado está com status '{statusNome}' e não pode ser processado pela IA. Entre em contato com um agente humano.",
                        StatusAtualizado = false
                    };
                }

                // 2. Buscar ou criar usuário Bot
                var botUsuarioId = await ObterOuCriarBotUsuarioAsync(tenantId, ct);

                // 3. Buscar histórico de interações
                var interacoes = await _chamadosRepository.ListarInteracoesAsync(chamadoId, tenantId, ct);
                var historicoMensagens = new List<MensagemHistorico>();
                foreach (var i in interacoes.OrderBy(i => i.DataCriacao))
                {
                    historicoMensagens.Add(new MensagemHistorico
                    {
                        Autor = i.AutorTipoUsuarioId == TipoUsuarios.Bot ? "IA" : "Cliente",
                        Mensagem = i.Mensagem ?? string.Empty,
                        Data = i.DataCriacao
                    });
                }

                // 4. Contar mensagens consecutivas fora do escopo (para chamados novos, será 0)
                var contadorForaEscopo = ContarMensagensConsecutivasForaEscopo(historicoMensagens);
                
                // 5. Construir contexto para a IA
                var contexto = ConstruirContexto(chamado, solicitante, historicoMensagens, contadorForaEscopo);

                // 6. Verificar se a IA está habilitada
                if (_aiProvider is OpenRouterService openRouter && !openRouter.EstaHabilitado())
                {
                    _logger.LogWarning("⚠️ OpenRouter não está habilitado. Verifique a configuração no appsettings.json");
                    throw new InvalidOperationException("Serviço de IA não está habilitado. Verifique a configuração do OpenRouter no appsettings.json.");
                }

                // 7. Chamar a IA e medir latência
                _logger.LogInformation("📤 Enviando contexto para IA. Tamanho do contexto: {Tamanho} caracteres", contexto.Length);
                var inicioChamada = DateTime.UtcNow;
                var respostaIA = await _aiProvider.ResponderAsync(contexto, ct);
                var latenciaMs = (int)(DateTime.UtcNow - inicioChamada).TotalMilliseconds;
                _logger.LogInformation("📥 Resposta da IA recebida. Tamanho: {Tamanho} caracteres, Modelo: {Modelo}, Latência: {Latencia}ms", 
                    respostaIA.Mensagem?.Length ?? 0, respostaIA.Modelo, latenciaMs);

                // 8. Verificar se a resposta indica que está fora do escopo
                var estaForaEscopo = RespostaIndicaForaEscopo(respostaIA.Mensagem ?? string.Empty);
                var mensagemFinal = respostaIA.Mensagem ?? string.Empty;
                
                // 9. Se estiver fora do escopo e for a 3ª vez consecutiva, forçar encerramento
                if (estaForaEscopo && contadorForaEscopo >= 2)
                {
                    _logger.LogWarning("⚠️ Cliente enviou {Contador} mensagens consecutivas fora do escopo. Forçando encerramento do chamado {ChamadoId}.", 
                        contadorForaEscopo + 1, chamadoId);
                    
                    // Modificar a mensagem para colocar o ⚠️ no início e não repetir no final
                    if (!mensagemFinal.StartsWith("⚠️"))
                    {
                        mensagemFinal = "⚠️ " + mensagemFinal;
                    }
                }
                
                // 10. Analisar resposta da IA e extrair ações
                var acoes = AnalisarRespostaIA(mensagemFinal, historicoMensagens);
                
                // Se for a 3ª vez fora do escopo, forçar encerramento
                if (estaForaEscopo && contadorForaEscopo >= 2)
                {
                    acoes.NovoStatus = (byte)StatusChamado.Fechado;
                }
                
                _logger.LogInformation("🔍 Ações extraídas da IA: NovoStatus={NovoStatus}, CriarNovoChamado={CriarNovoChamado}, ClientePediuHumano={ClientePediuHumano}, ForaEscopo={ForaEscopo}, ContadorForaEscopo={Contador}", 
                    acoes.NovoStatus, acoes.CriarNovoChamado != null, acoes.ClientePediuHumano, estaForaEscopo, contadorForaEscopo);

                // 7.5. Se cliente pediu para falar com humano, alterar status para Em Andamento
                if (acoes.ClientePediuHumano && chamado.StatusId == StatusChamado.Pendente)
                {
                    _logger.LogInformation("👤 Cliente pediu para falar com humano. Alterando status de Pendente para Em Andamento.");
                    await _chamadosRepository.AlterarStatusAsync(
                        chamadoId,
                        tenantId,
                        (byte)StatusChamado.EmAndamento,
                        botUsuarioId,
                        ct);
                    acoes.NovoStatus = (byte)StatusChamado.EmAndamento;
                }

                // 8. Adicionar resposta do bot como interação
                _logger.LogInformation("💬 Adicionando interação do bot ao chamado {ChamadoId}", chamadoId);
                long? interacaoId = null;
                try
                {
                    interacaoId = await AdicionarInteracaoBotAsync(
                        chamadoId,
                        tenantId,
                        botUsuarioId,
                        mensagemFinal,
                        respostaIA.Modelo,
                        respostaIA.Confianca,
                        respostaIA.ResumoRaciocinio,
                        ct);
                    _logger.LogInformation("✅ Interação do bot adicionada com sucesso. InteracaoId: {InteracaoId}", interacaoId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Erro ao adicionar interação do bot. Message: {Message}", ex.Message);
                    // Se falhar ao adicionar interação, ainda retornamos sucesso mas sem a mensagem
                    // Isso evita que o erro impeça o processamento completo
                }

                // 8.5. Salvar log de execução da IA
                try
                {
                    await SalvarIARunLogAsync(
                        tenantId,
                        chamadoId,
                        interacaoId,
                        respostaIA,
                        contexto,
                        latenciaMs,
                        ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Erro ao salvar log de execução da IA. Continuando...");
                }

                // 9. Atualizar status se necessário
                bool statusAtualizado = false;
                if (acoes.NovoStatus.HasValue && acoes.NovoStatus.Value != (byte)chamado.StatusId)
                {
                    await _chamadosRepository.AlterarStatusAsync(
                        chamadoId,
                        tenantId,
                        acoes.NovoStatus.Value,
                        botUsuarioId,
                        ct);
                    statusAtualizado = true;
                    _logger.LogInformation("✅ Status do chamado {ChamadoId} atualizado para {Status}", chamadoId, acoes.NovoStatus.Value);
                }

                // 10. Criar novos chamados relacionados se necessário
                if (acoes.CriarNovoChamado != null)
                {
                    try
                    {
                        _logger.LogInformation("📝 Criando novo chamado relacionado: {Titulo}", acoes.CriarNovoChamado.Titulo);
                        var novoChamado = await _chamadosService.CriarAsync(
                            tenantId,
                            new Contracts.Tickets.CriarChamadoRequest(
                                acoes.CriarNovoChamado.Titulo,
                                acoes.CriarNovoChamado.Descricao,
                                acoes.CriarNovoChamado.CategoriaId ?? chamado.CategoriaId ?? 1,
                                acoes.CriarNovoChamado.PrioridadeId ?? (byte)chamado.PrioridadeId,
                                (byte)chamado.CanalId,
                                chamado.SolicitanteUsuarioId,
                                null,
                                null
                            ),
                            ct);

                        _logger.LogInformation("✅ Novo chamado relacionado criado: {NovoChamadoId}", novoChamado?.ChamadoId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao criar novo chamado relacionado");
                    }
                }

                return new ProcessarChamadoResult
                {
                    Sucesso = true,
                    Mensagem = respostaIA.Mensagem ?? string.Empty,
                    StatusAtualizado = statusAtualizado,
                    NovoStatus = acoes.NovoStatus
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao processar chamado {ChamadoId} pelo Bot IA. Tipo: {Tipo}, Message: {Message}, InnerException: {InnerException}, StackTrace: {StackTrace}", 
                    chamadoId, ex.GetType().Name, ex.Message, ex.InnerException?.Message, ex.StackTrace);
                
                // Preservar a exceção original com mais contexto
                throw new InvalidOperationException(
                    $"Erro ao processar chamado {chamadoId} com IA: {ex.Message}" + 
                    (ex.InnerException != null ? $" (Detalhes: {ex.InnerException.Message})" : ""), 
                    ex);
            }
        }

        /// <summary>
        /// Processa uma nova mensagem do cliente em um chamado existente.
        /// </summary>
        public async Task<ProcessarMensagemResult> ProcessarMensagemClienteAsync(
            long chamadoId,
            int tenantId,
            string mensagemCliente,
            CancellationToken ct = default)
        {
            try
            {
                _logger.LogInformation("🤖 Processando mensagem do cliente no chamado {ChamadoId}", chamadoId);

                var chamado = await _chamadosRepository.ObterAsync(chamadoId, ct);
                if (chamado == null)
                {
                    throw new InvalidOperationException($"Chamado {chamadoId} não encontrado.");
                }

                // Verificar se o chamado foi criado por um cliente (IA não processa chamados de Agente ou Admin)
                var solicitante = await _usuariosRepository.ObterPorIdAsync(chamado.SolicitanteUsuarioId, ct);
                if (solicitante == null)
                {
                    _logger.LogWarning("⚠️ Solicitante {SolicitanteUsuarioId} não encontrado para o chamado {ChamadoId}. Pulando processamento IA.", chamado.SolicitanteUsuarioId, chamadoId);
                    throw new InvalidOperationException("Solicitante do chamado não encontrado.");
                }

                _logger.LogInformation("🔍 Solicitante encontrado: UsuarioId={UsuarioId}, TipoUsuarioId={TipoUsuarioId}, Nome={Nome}", 
                    solicitante.UsuarioId, solicitante.TipoUsuarioId, solicitante.NomeCompleto);

                if ((byte)solicitante.TipoUsuarioId != (byte)TipoUsuarios.Cliente)
                {
                    _logger.LogInformation("⏸️ Chamado {ChamadoId} não foi criado por um cliente (TipoUsuarioId={TipoUsuarioId}). IA não processa chamados de Agente ou Admin.", 
                        chamadoId, solicitante.TipoUsuarioId);
                    throw new InvalidOperationException($"Chamado criado por {solicitante.TipoUsuarioId}. A IA só processa chamados criados por clientes.");
                }

                // Verificar se o chamado pode ser processado pela IA
                if (!PodeProcessarChamado(chamado.StatusId))
                {
                    var statusNome = EnumHelperService.GetStatusNome(chamado.StatusId);
                    _logger.LogInformation("⏸️ Chamado {ChamadoId} com status {Status} não pode ser processado pela IA. Apenas agente humano pode atender.", chamadoId, statusNome);
                    throw new InvalidOperationException($"Este chamado está com status '{statusNome}' e não pode ser processado pela IA. Entre em contato com um agente humano.");
                }

                var botUsuarioId = await ObterOuCriarBotUsuarioAsync(tenantId, ct);
                var interacoes = await _chamadosRepository.ListarInteracoesAsync(chamadoId, tenantId, ct);

                var historicoMensagens = new List<MensagemHistorico>();
                foreach (var i in interacoes.OrderBy(i => i.DataCriacao))
                {
                    historicoMensagens.Add(new MensagemHistorico
                    {
                        Autor = i.AutorTipoUsuarioId == TipoUsuarios.Bot ? "IA" : "Cliente",
                        Mensagem = i.Mensagem ?? string.Empty,
                        Data = i.DataCriacao
                    });
                }

                // Adicionar a nova mensagem do cliente ao histórico
                historicoMensagens.Add(new MensagemHistorico
                {
                    Autor = "Cliente",
                    Mensagem = mensagemCliente,
                    Data = DateTime.UtcNow
                });

                // Contar quantas mensagens consecutivas do cliente foram fora do escopo
                var contadorForaEscopo = ContarMensagensConsecutivasForaEscopo(historicoMensagens);
                _logger.LogInformation("🔍 Contador de mensagens fora do escopo: {Contador}", contadorForaEscopo);

                var contexto = ConstruirContexto(chamado, solicitante, historicoMensagens, contadorForaEscopo);
                var inicioChamada = DateTime.UtcNow;
                var respostaIA = await _aiProvider.ResponderAsync(contexto, ct);
                var latenciaMs = (int)(DateTime.UtcNow - inicioChamada).TotalMilliseconds;
                // Verificar se a resposta da IA indica que está fora do escopo
                var estaForaEscopo = RespostaIndicaForaEscopo(respostaIA.Mensagem ?? string.Empty);
                var mensagemFinal = respostaIA.Mensagem ?? string.Empty;
                
                // Se estiver fora do escopo e for a 3ª vez consecutiva, encerrar o chamado
                if (estaForaEscopo && contadorForaEscopo >= 2)
                {
                    _logger.LogWarning("⚠️ Cliente enviou {Contador} mensagens consecutivas fora do escopo. Encerrando chamado {ChamadoId}.", 
                        contadorForaEscopo + 1, chamadoId);
                    
                    // Modificar a mensagem para colocar o ⚠️ no início e não repetir no final
                    if (!mensagemFinal.StartsWith("⚠️"))
                    {
                        mensagemFinal = "⚠️ " + mensagemFinal;
                    }
                }
                
                var acoes = AnalisarRespostaIA(mensagemFinal, historicoMensagens);
                
                // Se estiver fora do escopo e for a 3ª vez consecutiva, forçar encerramento
                if (estaForaEscopo && contadorForaEscopo >= 2)
                {
                    acoes.NovoStatus = (byte)StatusChamado.Fechado;
                }

                // Se cliente pediu para falar com humano, alterar status para Em Andamento
                if (acoes.ClientePediuHumano && chamado.StatusId == StatusChamado.Pendente)
                {
                    _logger.LogInformation("👤 Cliente pediu para falar com humano. Alterando status de Pendente para Em Andamento.");
                    await _chamadosRepository.AlterarStatusAsync(
                        chamadoId,
                        tenantId,
                        (byte)StatusChamado.EmAndamento,
                        botUsuarioId,
                        ct);
                    acoes.NovoStatus = (byte)StatusChamado.EmAndamento;
                }

                long? interacaoId = await AdicionarInteracaoBotAsync(
                    chamadoId,
                    tenantId,
                    botUsuarioId,
                    mensagemFinal,
                    respostaIA.Modelo,
                    respostaIA.Confianca,
                    respostaIA.ResumoRaciocinio,
                    ct);

                // Salvar log de execução da IA
                try
                {
                    await SalvarIARunLogAsync(
                        tenantId,
                        chamadoId,
                        interacaoId,
                        respostaIA,
                        contexto,
                        latenciaMs,
                        ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Erro ao salvar log de execução da IA. Continuando...");
                }

                bool statusAtualizado = false;
                if (acoes.NovoStatus.HasValue && acoes.NovoStatus.Value != (byte)chamado.StatusId)
                {
                    await _chamadosRepository.AlterarStatusAsync(
                        chamadoId,
                        tenantId,
                        acoes.NovoStatus.Value,
                        botUsuarioId,
                        ct);
                    statusAtualizado = true;
                }

                return new ProcessarMensagemResult
                {
                    Sucesso = true,
                    Mensagem = respostaIA.Mensagem ?? string.Empty,
                    StatusAtualizado = statusAtualizado,
                    NovoStatus = acoes.NovoStatus
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao processar mensagem do cliente no chamado {ChamadoId}", chamadoId);
                throw;
            }
        }

        private string ConstruirContexto(Chamado chamado, Usuario? solicitante, List<MensagemHistorico> historicoMensagens, int contadorForaEscopo = 0)
        {
            var sb = new StringBuilder();

            // Cumprimento baseado no horário
            var horaAtual = DateTime.UtcNow.AddHours(-3); // UTC-3 (Brasil)
            var cumprimento = ObterCumprimentoPorHorario(horaAtual);

            sb.AppendLine($"{cumprimento}! Você é um assistente de suporte técnico do CarTechAssist, responsável por atender chamados abertos por clientes.");
            sb.AppendLine();
            sb.AppendLine("SUAS RESPONSABILIDADES:");
            sb.AppendLine("1. Ler e entender o problema do cliente");
            sb.AppendLine("2. Tentar resolver de forma autônoma sempre que possível");
            sb.AppendLine("3. Manter uma conversa clara, educada e objetiva");
            sb.AppendLine("4. Atualizar o status do chamado conforme o andamento");
            sb.AppendLine("5. Encaminhar para um agente humano quando necessário");
            sb.AppendLine("6. Criar novos chamados relacionados quando fizer sentido");
            sb.AppendLine();
            sb.AppendLine("ESCOPO DE ATENDIMENTO - O QUE VOCÊ PODE FAZER:");
            sb.AppendLine("Você DEVE atender APENAS questões relacionadas a:");
            sb.AppendLine("- Problemas técnicos de TI (hardware, software, sistemas)");
            sb.AppendLine("- Infraestrutura de TI (servidores, redes, conectividade)");
            sb.AppendLine("- Logs e diagnóstico de sistemas");
            sb.AppendLine("- Problemas com o sistema CarTechAssist");
            sb.AppendLine("- Helpdesk e suporte técnico relacionado a sistemas");
            sb.AppendLine();
            sb.AppendLine("ESCOPO DE ATENDIMENTO - O QUE VOCÊ NÃO PODE FAZER:");
            sb.AppendLine("Você NÃO DEVE responder sobre:");
            sb.AppendLine("- Assuntos não relacionados a TI, Infraestrutura, Logs ou Sistemas");
            sb.AppendLine("- Questões financeiras, comerciais ou administrativas");
            sb.AppendLine("- Problemas pessoais ou questões não técnicas");
            sb.AppendLine("- Qualquer assunto fora do escopo de helpdesk técnico");
            sb.AppendLine();
            sb.AppendLine("Se o cliente perguntar algo fora do seu escopo, você DEVE:");
            sb.AppendLine("1. Educadamente informar que você só pode ajudar com questões técnicas de TI, Infraestrutura, Logs e Sistemas");
            sb.AppendLine("2. Sugerir que o cliente entre em contato com um agente humano para questões não técnicas");
            sb.AppendLine("3. Perguntar se há alguma questão técnica relacionada ao sistema que você possa ajudar");
            sb.AppendLine();
            
            // Adicionar avisos progressivos sobre encerramento
            if (contadorForaEscopo > 0)
            {
                sb.AppendLine("⚠️ ATENÇÃO - CONTROLE DE MENSAGENS FORA DO ESCOPO:");
                if (contadorForaEscopo == 1)
                {
                    sb.AppendLine("Esta é a SEGUNDA vez consecutiva que o cliente está enviando mensagens fora do escopo.");
                    sb.AppendLine("Você DEVE avisar o cliente que, se ele continuar enviando mensagens fora do escopo de atendimento técnico, o chamado será ENCERRADO.");
                    sb.AppendLine("Seja educado mas firme: 'Se você continuar enviando mensagens que não são relacionadas a questões técnicas, este chamado será encerrado. Por favor, envie apenas questões técnicas relacionadas a TI, Infraestrutura, Logs ou Sistemas.'");
                }
                else if (contadorForaEscopo >= 2)
                {
                    sb.AppendLine("Esta é a TERCEIRA ou mais vez consecutiva que o cliente está enviando mensagens fora do escopo.");
                    sb.AppendLine("Você DEVE ENCERRAR o chamado imediatamente usando [STATUS:5].");
                    sb.AppendLine("Informe ao cliente que, como ele continuou enviando mensagens fora do escopo, o chamado será encerrado.");
                    sb.AppendLine("IMPORTANTE: Coloque o símbolo ⚠️ no INÍCIO da sua mensagem (ex: '⚠️ Como você continuou enviando mensagens fora do escopo de atendimento técnico, este chamado será encerrado. Se precisar de ajuda técnica, por favor, crie um novo chamado.').");
                    sb.AppendLine("NÃO repita a mensagem no final - apenas coloque o ⚠️ no início da sua resposta.");
                }
                sb.AppendLine();
            }
            sb.AppendLine("INFORMAÇÕES DO CHAMADO:");
            sb.AppendLine($"- Número: {chamado.Numero}");
            sb.AppendLine($"- Título: {chamado.Titulo}");
            sb.AppendLine($"- Descrição: {chamado.Descricao ?? "Sem descrição"}");
            sb.AppendLine($"- Status Atual: {EnumHelperService.GetStatusNome(chamado.StatusId)}");
            sb.AppendLine($"- Prioridade: {EnumHelperService.GetPrioridadeNome(chamado.PrioridadeId)}");
            sb.AppendLine($"- Cliente: {solicitante?.NomeCompleto ?? "Desconhecido"}");
            sb.AppendLine();
            sb.AppendLine("STATUS DISPONÍVEIS:");
            sb.AppendLine("- 1 = Aberto");
            sb.AppendLine("- 2 = Em Andamento");
            sb.AppendLine("- 3 = Pendente");
            sb.AppendLine("- 4 = Resolvido");
            sb.AppendLine("- 5 = Fechado");
            sb.AppendLine("- 6 = Cancelado");
            sb.AppendLine();
            sb.AppendLine("DIRETRIZES:");
            sb.AppendLine("- Se você acreditar que o problema foi resolvido, pergunte explicitamente ao cliente se a situação foi solucionada.");
            sb.AppendLine("- Se o cliente confirmar que está tudo certo, atualize o status para 5 (Fechado).");
            sb.AppendLine("- Se o cliente pedir para falar com um humano, agente, atendente ou pessoa (ex: 'quero falar com alguém', 'me passe um humano', 'quero um atendente'), você DEVE:");
            sb.AppendLine("  * Imediatamente alterar o status para 2 (Em Andamento)");
            sb.AppendLine("  * Informar que o chamado foi encaminhado para um agente humano");
            sb.AppendLine("  * Após isso, a IA não poderá mais atender este chamado");
            sb.AppendLine("- Se o cliente disser que ainda não está resolvido (mas não pediu humano), pergunte se ele deseja que o chamado seja direcionado para um agente humano.");
            sb.AppendLine("- Se faltar informação essencial, pergunte ao cliente de forma clara e objetiva e atualize o status para 3 (Pendente).");
            sb.AppendLine("- Se surgir outra demanda que precise ser tratada separadamente, informe que você irá criar um novo chamado relacionado.");
            sb.AppendLine();
            sb.AppendLine("FORMATO DE RESPOSTA:");
            sb.AppendLine("Responda normalmente ao cliente. Se precisar atualizar o status ou criar novo chamado, use as seguintes tags no final da sua resposta:");
            sb.AppendLine("- [STATUS:5] para fechar o chamado");
            sb.AppendLine("- [STATUS:2] para encaminhar para agente");
            sb.AppendLine("- [STATUS:3] para marcar como pendente (aguardando resposta do cliente)");
            sb.AppendLine("- [NOVO_CHAMADO:Título|Descrição|CategoriaId|PrioridadeId] para criar um novo chamado relacionado");
            sb.AppendLine();
            sb.AppendLine("HISTÓRICO DE CONVERSA:");
            if (historicoMensagens.Any())
            {
                foreach (var msg in historicoMensagens)
                {
                    sb.AppendLine($"[{msg.Autor}] {msg.Mensagem}");
                }
            }
            else
            {
                sb.AppendLine("Nenhuma mensagem anterior.");
            }
            sb.AppendLine();
            sb.AppendLine("Agora, analise o chamado e responda ao cliente de forma clara e objetiva.");

            return sb.ToString();
        }

        private class MensagemHistorico
        {
            public string Autor { get; set; } = string.Empty;
            public string Mensagem { get; set; } = string.Empty;
            public DateTime Data { get; set; }
        }

        private AcoesIA AnalisarRespostaIA(string resposta, List<MensagemHistorico> historicoMensagens)
        {
            var acoes = new AcoesIA();

            // Verificar se cliente pediu para falar com humano (na última mensagem do cliente)
            var ultimaMensagemCliente = historicoMensagens
                .Where(m => m.Autor == "Cliente")
                .OrderByDescending(m => m.Data)
                .FirstOrDefault();

            if (ultimaMensagemCliente != null)
            {
                var mensagemLower = ultimaMensagemCliente.Mensagem.ToLowerInvariant();
                var palavrasChave = new[] { "humano", "pessoa", "atendente", "agente", "alguém", "alguem", "operador", "suporte humano" };
                if (palavrasChave.Any(palavra => mensagemLower.Contains(palavra)))
                {
                    acoes.ClientePediuHumano = true;
                    _logger.LogInformation("🔍 Detectado pedido do cliente para falar com humano na mensagem: {Mensagem}", ultimaMensagemCliente.Mensagem);
                }
            }

            // Extrair status
            var statusMatch = System.Text.RegularExpressions.Regex.Match(resposta, @"\[STATUS:(\d+)\]");
            if (statusMatch.Success && byte.TryParse(statusMatch.Groups[1].Value, out var status))
            {
                acoes.NovoStatus = status;
            }

            // Extrair novo chamado
            var novoChamadoMatch = System.Text.RegularExpressions.Regex.Match(resposta, @"\[NOVO_CHAMADO:(.+?)\|(.+?)\|(\d+)?\|(\d+)?\]");
            if (novoChamadoMatch.Success)
            {
                acoes.CriarNovoChamado = new NovoChamadoInfo
                {
                    Titulo = novoChamadoMatch.Groups[1].Value,
                    Descricao = novoChamadoMatch.Groups[2].Value,
                    CategoriaId = novoChamadoMatch.Groups[3].Success && int.TryParse(novoChamadoMatch.Groups[3].Value, out var catId) ? catId : null,
                    PrioridadeId = novoChamadoMatch.Groups[4].Success && byte.TryParse(novoChamadoMatch.Groups[4].Value, out var priId) ? priId : null
                };
            }

            return acoes;
        }

        private bool PodeProcessarChamado(StatusChamado statusId)
        {
            // IA não pode processar chamados finalizados ou em andamento (após pedido de humano)
            return statusId != StatusChamado.Resolvido 
                && statusId != StatusChamado.Fechado 
                && statusId != StatusChamado.Cancelado
                && statusId != StatusChamado.EmAndamento; // Em Andamento significa que foi encaminhado para humano
        }

        private string ObterCumprimentoPorHorario(DateTime hora)
        {
            var horaLocal = hora.Hour;
            if (horaLocal >= 5 && horaLocal < 12)
                return "Bom dia";
            else if (horaLocal >= 12 && horaLocal < 18)
                return "Boa tarde";
            else
                return "Boa noite";
        }

        /// <summary>
        /// Conta quantas mensagens consecutivas do cliente foram fora do escopo.
        /// Verifica as últimas interações para detectar padrão de mensagens fora do escopo.
        /// </summary>
        private int ContarMensagensConsecutivasForaEscopo(List<MensagemHistorico> historicoMensagens)
        {
            if (historicoMensagens == null || historicoMensagens.Count < 2)
                return 0;

            var contador = 0;
            var mensagensOrdenadas = historicoMensagens.OrderByDescending(m => m.Data).ToList();

            // Percorrer do mais recente para o mais antigo
            for (int i = 0; i < mensagensOrdenadas.Count - 1; i++)
            {
                var mensagemAtual = mensagensOrdenadas[i];
                var mensagemAnterior = mensagensOrdenadas[i + 1];

                // Se a mensagem atual é da IA e a anterior é do Cliente
                if (mensagemAtual.Autor == "IA" && mensagemAnterior.Autor == "Cliente")
                {
                    // Verificar se a resposta da IA indica que está fora do escopo
                    if (RespostaIndicaForaEscopo(mensagemAtual.Mensagem))
                    {
                        contador++;
                    }
                    else
                    {
                        // Se encontrou uma resposta que não está fora do escopo, para a contagem
                        break;
                    }
                }
            }

            return contador;
        }

        /// <summary>
        /// Verifica se a resposta da IA indica que o cliente está fora do escopo de atendimento.
        /// </summary>
        private bool RespostaIndicaForaEscopo(string respostaIA)
        {
            if (string.IsNullOrWhiteSpace(respostaIA))
                return false;

            var respostaLower = respostaIA.ToLowerInvariant();
            
            // Palavras-chave que indicam que a IA está informando que está fora do escopo
            var indicadoresForaEscopo = new[]
            {
                "não posso ajudar",
                "não posso auxiliar",
                "meu escopo",
                "escopo de atendimento",
                "questões técnicas",
                "questões não técnicas",
                "fora do escopo",
                "não relacionadas a ti",
                "não relacionadas a infraestrutura",
                "agente humano para questões não técnicas",
                "só posso ajudar com questões técnicas",
                "apenas questões técnicas",
                "limita a questões técnicas",
                "não posso ajudar com",
                "não posso auxiliar com",
                "questões pessoais",
                "questões não técnicas"
            };

            return indicadoresForaEscopo.Any(indicador => respostaLower.Contains(indicador));
        }

        private async Task<int> ObterOuCriarBotUsuarioAsync(int tenantId, CancellationToken ct)
        {
            // Buscar usuário bot existente
            var botUsuario = await _usuariosRepository.ObterPorLoginAsync(tenantId, "BOT_IA", ct);
            if (botUsuario != null)
            {
                _logger.LogInformation("✅ Usuário Bot encontrado. UsuarioId: {UsuarioId}", botUsuario.UsuarioId);
                return botUsuario.UsuarioId;
            }

            // Criar usuário bot se não existir
            _logger.LogInformation("🔧 Criando usuário Bot para tenant {TenantId}", tenantId);
            
            // Gerar hash e salt para o Bot (usando uma senha aleatória que nunca será usada)
            // O Bot não faz login, mas precisa ter hash e salt válidos para passar na validação
            var (hash, salt) = GerarHashSenhaBot();

            var novoBot = new Usuario
            {
                TenantId = tenantId,
                TipoUsuarioId = TipoUsuarios.Bot,
                Login = "BOT_IA",
                NomeCompleto = "Bot IA - CarTechAssist",
                Email = null,
                Telefone = null,
                HashSenha = hash,
                SaltSenha = salt,
                PrecisaTrocarSenha = false,
                Ativo = true,
                DataCriacao = DateTime.UtcNow,
                Excluido = false
            };

            try
            {
                var botCriado = await _usuariosRepository.CriarAsync(novoBot, ct);
                _logger.LogInformation("✅ Usuário Bot criado com sucesso. UsuarioId: {UsuarioId}", botCriado.UsuarioId);
                return botCriado.UsuarioId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao criar usuário Bot. Message: {Message}, StackTrace: {StackTrace}", ex.Message, ex.StackTrace);
                // Tentar buscar novamente (pode ter sido criado por outra thread)
                botUsuario = await _usuariosRepository.ObterPorLoginAsync(tenantId, "BOT_IA", ct);
                if (botUsuario != null)
                {
                    _logger.LogInformation("✅ Usuário Bot encontrado após erro. UsuarioId: {UsuarioId}", botUsuario.UsuarioId);
                    return botUsuario.UsuarioId;
                }
                throw new InvalidOperationException($"Não foi possível criar ou encontrar o usuário Bot. Erro: {ex.Message}", ex);
            }
        }

        private static (byte[] hash, byte[] salt) GerarHashSenhaBot()
        {
            // Gerar hash e salt para o Bot usando uma senha aleatória
            // O Bot nunca fará login, então a senha não importa
            using var hmac = new System.Security.Cryptography.HMACSHA512();
            var salt = hmac.Key;
            // Usar uma string fixa como senha (nunca será usada para login)
            var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes("BOT_IA_SYSTEM_PASSWORD_NEVER_USED"));
            return (hash, salt);
        }

        private async Task<long?> AdicionarInteracaoBotAsync(
            long chamadoId,
            int tenantId,
            int botUsuarioId,
            string mensagem,
            string modelo,
            decimal? confianca,
            string? resumoRaciocinio,
            CancellationToken ct)
        {
            // Remover tags de ação da mensagem antes de salvar
            var mensagemLimpa = System.Text.RegularExpressions.Regex.Replace(mensagem, @"\[STATUS:\d+\]", "");
            mensagemLimpa = System.Text.RegularExpressions.Regex.Replace(mensagemLimpa, @"\[NOVO_CHAMADO:.*?\]", "");

            // Usar o método existente de adicionar interação IA
            var chamadoAtualizado = await _chamadosRepository.AdicionarInteracaoIaAsync(
                chamadoId,
                tenantId,
                modelo,
                mensagemLimpa,
                confianca,
                resumoRaciocinio,
                "OpenRouter",
                null,
                null,
                null,
                ct);

            // Buscar a interação recém-criada para obter o ID
            var interacoes = await _chamadosRepository.ListarInteracoesAsync(chamadoId, tenantId, ct);
            var ultimaInteracao = interacoes
                .Where(i => i.AutorTipoUsuarioId == TipoUsuarios.Bot)
                .OrderByDescending(i => i.DataCriacao)
                .FirstOrDefault();

            return ultimaInteracao?.InteracaoId;
        }

        private async Task SalvarIARunLogAsync(
            int tenantId,
            long? chamadoId,
            long? interacaoId,
            (string Provedor, string Modelo, string Mensagem, decimal? Confianca, string? ResumoRaciocinio, int? InputTokens, int? outputTokens, decimal? CustoUsd) respostaIA,
            string prompt,
            int latenciaMs,
            CancellationToken ct)
        {
            if (_iaRunLogRepository == null)
            {
                _logger.LogDebug("⚠️ IIARunLogRepository não está registrado. Pulando salvamento de log.");
                return;
            }

            try
            {
                // Calcular hash do prompt
                byte[]? promptHash = null;
                if (!string.IsNullOrEmpty(prompt))
                {
                    using var sha256 = System.Security.Cryptography.SHA256.Create();
                    promptHash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(prompt));
                }

                var runLog = new IARunLog
                {
                    TenantId = tenantId,
                    ChamadoId = chamadoId,
                    InteracaoId = interacaoId,
                    Provedor = respostaIA.Provedor,
                    Modelo = respostaIA.Modelo,
                    PromptHash = promptHash,
                    InputTokens = respostaIA.InputTokens,
                    OutputTokens = respostaIA.outputTokens,
                    LatenciaMs = latenciaMs,
                    CustoUSD = respostaIA.CustoUsd,
                    Confianca = respostaIA.Confianca,
                    TipoResultado = "Sucesso",
                    DataCriacao = DateTime.UtcNow
                };

                var runId = await _iaRunLogRepository.CriarAsync(runLog, ct);
                _logger.LogInformation("✅ Log de execução da IA salvo. IARunId: {IARunId}", runId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao salvar log de execução da IA");
                throw;
            }
        }

        private class AcoesIA
        {
            public byte? NovoStatus { get; set; }
            public NovoChamadoInfo? CriarNovoChamado { get; set; }
            public bool ClientePediuHumano { get; set; }
        }

        private class NovoChamadoInfo
        {
            public string Titulo { get; set; } = string.Empty;
            public string Descricao { get; set; } = string.Empty;
            public int? CategoriaId { get; set; }
            public byte? PrioridadeId { get; set; }
        }
    }

    public class ProcessarChamadoResult
    {
        public bool Sucesso { get; set; }
        public string Mensagem { get; set; } = string.Empty;
        public bool StatusAtualizado { get; set; }
        public byte? NovoStatus { get; set; }
    }

    public class ProcessarMensagemResult
    {
        public bool Sucesso { get; set; }
        public string Mensagem { get; set; } = string.Empty;
        public bool StatusAtualizado { get; set; }
        public byte? NovoStatus { get; set; }
    }
}

