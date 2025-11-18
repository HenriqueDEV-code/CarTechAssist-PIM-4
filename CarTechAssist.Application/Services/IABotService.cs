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

        public IABotService(
            IAiProvider aiProvider,
            IChamadosRepository chamadosRepository,
            IUsuariosRepository usuariosRepository,
            ILogger<IABotService> logger,
            ChamadosService chamadosService)
        {
            _aiProvider = aiProvider;
            _chamadosRepository = chamadosRepository;
            _usuariosRepository = usuariosRepository;
            _logger = logger;
            _chamadosService = chamadosService;
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

                // 4. Construir contexto para a IA
                var contexto = ConstruirContexto(chamado, solicitante, historicoMensagens);

                // 5. Chamar a IA
                _logger.LogInformation("📤 Enviando contexto para IA. Tamanho do contexto: {Tamanho} caracteres", contexto.Length);
                var respostaIA = await _aiProvider.ResponderAsync(contexto, ct);
                _logger.LogInformation("📥 Resposta da IA recebida. Tamanho: {Tamanho} caracteres, Modelo: {Modelo}", 
                    respostaIA.Mensagem?.Length ?? 0, respostaIA.Modelo);

                // 6. Analisar resposta da IA e extrair ações
                var acoes = AnalisarRespostaIA(respostaIA.Mensagem ?? string.Empty);
                _logger.LogInformation("🔍 Ações extraídas da IA: NovoStatus={NovoStatus}, CriarNovoChamado={CriarNovoChamado}", 
                    acoes.NovoStatus, acoes.CriarNovoChamado != null);

                // 7. Adicionar resposta do bot como interação
                _logger.LogInformation("💬 Adicionando interação do bot ao chamado {ChamadoId}", chamadoId);
                await AdicionarInteracaoBotAsync(
                    chamadoId,
                    tenantId,
                    botUsuarioId,
                    respostaIA.Mensagem ?? string.Empty,
                    respostaIA.Modelo,
                    respostaIA.Confianca,
                    respostaIA.ResumoRaciocinio,
                    ct);
                _logger.LogInformation("✅ Interação do bot adicionada com sucesso");

                // 8. Atualizar status se necessário
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

                // 9. Criar novos chamados relacionados se necessário
                if (acoes.CriarNovoChamado != null)
                {
                    try
                    {
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
                _logger.LogError(ex, "❌ Erro ao processar chamado {ChamadoId} pelo Bot IA", chamadoId);
                throw;
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

                var botUsuarioId = await ObterOuCriarBotUsuarioAsync(tenantId, ct);
                var interacoes = await _chamadosRepository.ListarInteracoesAsync(chamadoId, tenantId, ct);
                var solicitante = await _usuariosRepository.ObterPorIdAsync(chamado.SolicitanteUsuarioId, ct);

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

                var contexto = ConstruirContexto(chamado, solicitante, historicoMensagens);
                var respostaIA = await _aiProvider.ResponderAsync(contexto, ct);
                var acoes = AnalisarRespostaIA(respostaIA.Mensagem);

                await AdicionarInteracaoBotAsync(
                    chamadoId,
                    tenantId,
                    botUsuarioId,
                    respostaIA.Mensagem,
                    respostaIA.Modelo,
                    respostaIA.Confianca,
                    respostaIA.ResumoRaciocinio,
                    ct);

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
                    Mensagem = respostaIA.Mensagem,
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

        private string ConstruirContexto(Chamado chamado, Usuario? solicitante, List<MensagemHistorico> historicoMensagens)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Você é um assistente de suporte técnico do CarTechAssist, responsável por atender chamados abertos por clientes.");
            sb.AppendLine();
            sb.AppendLine("SUAS RESPONSABILIDADES:");
            sb.AppendLine("1. Ler e entender o problema do cliente");
            sb.AppendLine("2. Tentar resolver de forma autônoma sempre que possível");
            sb.AppendLine("3. Manter uma conversa clara, educada e objetiva");
            sb.AppendLine("4. Atualizar o status do chamado conforme o andamento");
            sb.AppendLine("5. Encaminhar para um agente humano quando necessário");
            sb.AppendLine("6. Criar novos chamados relacionados quando fizer sentido");
            sb.AppendLine();
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
            sb.AppendLine("- Se o cliente disser que ainda não está resolvido, pergunte se ele deseja que o chamado seja direcionado para um agente humano.");
            sb.AppendLine("- Se ele concordar, altere o status para 2 (Em Andamento) para indicar que um agente humano assumirá.");
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

        private AcoesIA AnalisarRespostaIA(string resposta)
        {
            var acoes = new AcoesIA();

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

        private async Task AdicionarInteracaoBotAsync(
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
            await _chamadosRepository.AdicionarInteracaoIaAsync(
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
        }

        private class AcoesIA
        {
            public byte? NovoStatus { get; set; }
            public NovoChamadoInfo? CriarNovoChamado { get; set; }
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

