using System.IO;
using CarTechAssist.Contracts.Common;
using CarTechAssist.Contracts.Enums;
using CarTechAssist.Contracts.Tickets;
using CarTechAssist.Domain.Entities;
using CarTechAssist.Domain.Enums;
using CarTechAssist.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CarTechAssist.Application.Services
{
    public class ChamadosService
    {
        private readonly IChamadosRepository _chamadosRepository;
        private readonly IAnexosReposity _anexosRepository;
        private readonly IFeedbackRepository _feedbackRepository;
        private readonly IUsuariosRepository _usuariosRepository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ChamadosService> _logger;
        private readonly InputSanitizer? _inputSanitizer;

        public ChamadosService(
            IChamadosRepository chamadosRepository,
            IAnexosReposity anexosRepository,
            IFeedbackRepository feedbackRepository,
            IUsuariosRepository usuariosRepository,
            IConfiguration configuration,
            ILogger<ChamadosService> logger,
            InputSanitizer? inputSanitizer = null)
        {
            _chamadosRepository = chamadosRepository;
            _anexosRepository = anexosRepository;
            _feedbackRepository = feedbackRepository;
            _usuariosRepository = usuariosRepository;
            _configuration = configuration;
            _logger = logger;
            _inputSanitizer = inputSanitizer;
        }

        public async Task<PagedResult<TicketView>> ListarAsync(
            int tenantId,
            byte? statusId,
            int? responsavelUsuarioId,
            int? solicitanteUsuarioId,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            var (items, total) = await _chamadosRepository.ListaAsync(
                tenantId, statusId, responsavelUsuarioId, solicitanteUsuarioId, page, pageSize, ct);

            var ticketViews = items.Select(c => new TicketView(
                c.ChamadoId,
                c.Numero,
                c.Titulo,
                EnumHelperService.GetStatusNome(c.StatusId), // CORREÇÃO: Nome legível do status
                EnumHelperService.GetPrioridadeNome(c.PrioridadeId), // CORREÇÃO: Nome legível da prioridade
                c.IA_FeedbackScore.HasValue ? (IAFeedbackScoreDto)(byte)c.IA_FeedbackScore.Value : null,
                c.DataCriacao,
                EnumHelperService.GetCanalNome(c.CanalId), // CORREÇÃO: Nome legível do canal
                c.CategoriaId, // CORREÇÃO: Incluir CategoriaId
                !string.IsNullOrEmpty(c.Descricao) && c.Descricao.Length > 100 
                    ? c.Descricao.Substring(0, 100) + "..." 
                    : c.Descricao, // CORREÇÃO: Descrição resumida para listagem
                c.SolicitanteUsuarioId, // CORREÇÃO: Incluir ID do solicitante
                c.ResponsavelUsuarioId // CORREÇÃO: Incluir ID do responsável
            )).ToList();

            return new PagedResult<TicketView>(ticketViews, total, page, pageSize);
        }

        public async Task<ChamadoDetailDto?> ObterAsync(
            long chamadoId, 
            int tenantId,
            int? usuarioId = null,
            byte? tipoUsuarioId = null,
            CancellationToken ct = default)
        {
            var chamado = await _chamadosRepository.ObterAsync(chamadoId, ct);
            if (chamado == null) return null;

            if (chamado.TenantId != tenantId)
                throw new UnauthorizedAccessException("Chamado não pertence ao tenant informado.");

            if (tipoUsuarioId == 1 && usuarioId.HasValue)
            {
                if (chamado.SolicitanteUsuarioId != usuarioId.Value)
                    throw new UnauthorizedAccessException("Você não tem permissão para visualizar este chamado.");
            }

            return new ChamadoDetailDto(
                chamado.ChamadoId,
                chamado.Numero,
                chamado.Titulo,
                chamado.Descricao,
                chamado.CategoriaId,
                (byte)chamado.StatusId,
                (byte)chamado.PrioridadeId,
                (byte)chamado.CanalId,
                chamado.SolicitanteUsuarioId,
                chamado.ResponsavelUsuarioId,
                chamado.DataCriacao,
                chamado.DataAtualizacao,
                EnumHelperService.GetStatusNome(chamado.StatusId),
                EnumHelperService.GetPrioridadeNome(chamado.PrioridadeId),
                EnumHelperService.GetCanalNome(chamado.CanalId)
            );
        }

        public async Task<ChamadoDetailDto> CriarAsync(
            int tenantId,
            CriarChamadoRequest request,
            CancellationToken ct)
        {

            _logger.LogInformation("Iniciando criação de chamado. TenantId: {TenantId}, SolicitanteUsuarioId: {SolicitanteUsuarioId}, ResponsavelUsuarioId: {ResponsavelUsuarioId}",
                tenantId, request.SolicitanteUsuarioId, request.ResponsavelUsuarioId?.ToString() ?? "null");

            var solicitante = await _usuariosRepository.ObterPorIdAsync(request.SolicitanteUsuarioId, ct);
            if (solicitante == null)
            {
                _logger.LogError("Usuário solicitante {UsuarioId} não encontrado no banco de dados", request.SolicitanteUsuarioId);
                throw new ArgumentException($"Usuário solicitante {request.SolicitanteUsuarioId} não encontrado.");
            }
            
            _logger.LogInformation("Usuário solicitante encontrado. UsuarioId: {UsuarioId}, TenantId: {TenantIdBanco}, Ativo: {Ativo}, Excluido: {Excluido}",
                solicitante.UsuarioId, solicitante.TenantId, solicitante.Ativo, solicitante.Excluido);
            
            if (solicitante.TenantId != tenantId)
            {
                _logger.LogError("TenantId não corresponde. Esperado: {TenantIdEsperado}, Encontrado: {TenantIdBanco}",
                    tenantId, solicitante.TenantId);
                throw new UnauthorizedAccessException($"Usuário solicitante não pertence ao tenant {tenantId}.");
            }
            if (!solicitante.Ativo || solicitante.Excluido)
            {
                _logger.LogError("Usuário solicitante está inativo ou excluído. Ativo: {Ativo}, Excluido: {Excluido}",
                    solicitante.Ativo, solicitante.Excluido);
                throw new ArgumentException($"Usuário solicitante {request.SolicitanteUsuarioId} está inativo ou excluído.");
            }

            if (request.ResponsavelUsuarioId.HasValue)
            {
                var responsavel = await _usuariosRepository.ObterPorIdAsync(request.ResponsavelUsuarioId.Value, ct);
                if (responsavel == null)
                {
                    throw new ArgumentException($"Usuário responsável {request.ResponsavelUsuarioId.Value} não encontrado.");
                }
                if (responsavel.TenantId != tenantId)
                {
                    throw new UnauthorizedAccessException($"Usuário responsável não pertence ao tenant {tenantId}.");
                }
                if (!responsavel.Ativo || responsavel.Excluido)
                {
                    throw new ArgumentException($"Usuário responsável {request.ResponsavelUsuarioId.Value} está inativo ou excluído.");
                }

                if (responsavel.TipoUsuarioId != Domain.Enums.TipoUsuarios.Tecnico && 
                    responsavel.TipoUsuarioId != Domain.Enums.TipoUsuarios.Administrador)
                {
                    throw new ArgumentException($"Usuário responsável deve ser Técnico ou Administrador.");
                }
            }

            try
            {
                _logger.LogInformation("Chamando repository para criar chamado. TenantId: {TenantId}, SolicitanteUsuarioId: {SolicitanteUsuarioId}",
                    tenantId, request.SolicitanteUsuarioId);

                var tituloSanitizado = _inputSanitizer?.Sanitize(request.Titulo) ?? request.Titulo;
                var descricaoSanitizada = _inputSanitizer?.Sanitize(request.Descricao) ?? request.Descricao; // Descricao agora é obrigatório, não precisa ?? string.Empty

                var slaEstimadoFim = request.SLA_EstimadoFim ?? CalcularSLAEstimado(request.PrioridadeId);
                
                var chamado = await _chamadosRepository.CriarAsync(
                    tenantId,
                    tituloSanitizado,
                    descricaoSanitizada,
                    request.CategoriaId,
                    request.PrioridadeId,
                    request.CanalId,
                    request.SolicitanteUsuarioId,
                    request.ResponsavelUsuarioId,
                    slaEstimadoFim,
                    ct);

                _logger.LogInformation("✅ Chamado criado com sucesso no repository! ChamadoId: {ChamadoId}, Numero: {Numero}, SLA_EstimadoFim: {SLA}",
                    chamado.ChamadoId, chamado.Numero, slaEstimadoFim);

                return new ChamadoDetailDto(
                    chamado.ChamadoId,
                    chamado.Numero,
                    chamado.Titulo,
                    chamado.Descricao,
                    chamado.CategoriaId,
                    (byte)chamado.StatusId,
                    (byte)chamado.PrioridadeId,
                    (byte)chamado.CanalId,
                    chamado.SolicitanteUsuarioId,
                    chamado.ResponsavelUsuarioId,
                    chamado.DataCriacao,
                    chamado.DataAtualizacao,
                    EnumHelperService.GetStatusNome(chamado.StatusId), // CORREÇÃO: Nome legível
                    EnumHelperService.GetPrioridadeNome(chamado.PrioridadeId), // CORREÇÃO: Nome legível
                    EnumHelperService.GetCanalNome(chamado.CanalId) // CORREÇÃO: Nome legível
                );
            }
            catch (Exception ex) when (ex.Message.Contains("FOREIGN KEY") || ex.Message.Contains("547"))
            {
                _logger.LogError(ex, "❌ ERRO SQL: FOREIGN KEY constraint violation. Solicitante: {Solicitante}, Responsavel: {Responsavel}, Mensagem: {Message}",
                    request.SolicitanteUsuarioId, request.ResponsavelUsuarioId?.ToString() ?? "null", ex.Message);
                throw new ArgumentException(
                    "Erro ao criar chamado: usuário informado não existe ou não pertence ao tenant. " +
                    $"Solicitante: {request.SolicitanteUsuarioId}, " +
                    $"Responsável: {request.ResponsavelUsuarioId?.ToString() ?? "não informado"}. " +
                    "Verifique se os IDs estão corretos.", ex);
            }
            catch (Exception ex) when (ex.Message.Contains("ChamadoId") && ex.Message.Contains("NULL"))
            {
                _logger.LogError(ex, "❌ ERRO SQL: Tentativa de inserir NULL em ChamadoId no histórico. Mensagem: {Message}", ex.Message);
                throw new InvalidOperationException(
                    "Erro ao criar chamado: falha na inserção do histórico de status. " +
                    "A stored procedure pode estar tentando inserir histórico antes do ChamadoId estar disponível. " +
                    "Verifique a stored procedure 'core.usp_Chamado_Criar'.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ERRO INESPERADO ao criar chamado no repository. Tipo: {Type}, Mensagem: {Message}, StackTrace: {StackTrace}",
                    ex.GetType().Name, ex.Message, ex.StackTrace);
                throw;
            }
        }

        public async Task<IReadOnlyList<InteracaoDto>> ListarInteracoesAsync(
            long chamadoId,
            int tenantId,
            CancellationToken ct)
        {
            var interacoes = await _chamadosRepository.ListarInteracoesAsync(chamadoId, tenantId, ct);


            return interacoes.Select(i => new InteracaoDto(
                i.InteracaoId,
                i.ChamadoId,
                i.AutorUsuarioId,
                GetAutorNome(i.AutorTipoUsuarioId, i.AutorUsuarioId), // Nome baseado no tipo
                (byte)i.AutorTipoUsuarioId,
                (byte)i.CanalId,
                i.Mensagem,
                i.Interna,
                i.IA_Gerada,
                i.IA_Modelo,
                i.IA_Confianca,
                i.IA_ResumoRaciocinio,
                i.DataCriacao
            )).ToList();
        }

        private static string GetAutorNome(Domain.Enums.TipoUsuarios tipoUsuario, int? usuarioId)
        {
            var tipoNome = tipoUsuario switch
            {
                Domain.Enums.TipoUsuarios.Cliente => "Cliente",
                Domain.Enums.TipoUsuarios.Tecnico => "Agente",
                Domain.Enums.TipoUsuarios.Administrador => "Admin",
                Domain.Enums.TipoUsuarios.Bot => "Bot",
                _ => "Usuário"
            };

            return usuarioId.HasValue ? $"{tipoNome} #{usuarioId}" : tipoNome;
        }

        public async Task<ChamadoDetailDto> AdicionarInteracaoAsync(
            int tenantId,
            long chamadoId,
            int usuarioId,
            AdicionarInteracaoRequest request,
            CancellationToken ct)
        {

            var mensagemSanitizada = _inputSanitizer?.SanitizePreservingLineBreaks(request.Mensagem) ?? request.Mensagem;
            
            var chamado = await _chamadosRepository.AdicionarInteracaoAsync(
                chamadoId, tenantId, usuarioId, mensagemSanitizada, ct);

            return new ChamadoDetailDto(
                chamado.ChamadoId,
                chamado.Numero,
                chamado.Titulo,
                chamado.Descricao,
                chamado.CategoriaId,
                (byte)chamado.StatusId,
                (byte)chamado.PrioridadeId,
                (byte)chamado.CanalId,
                chamado.SolicitanteUsuarioId,
                chamado.ResponsavelUsuarioId,
                chamado.DataCriacao,
                chamado.DataAtualizacao,
                EnumHelperService.GetStatusNome(chamado.StatusId),
                EnumHelperService.GetPrioridadeNome(chamado.PrioridadeId),
                EnumHelperService.GetCanalNome(chamado.CanalId)
            );
        }

        public async Task<ChamadoDetailDto> AlterarStatusAsync(
            int tenantId,
            long chamadoId,
            int usuarioId,
            AlterarStatusRequest request,
            CancellationToken ct)
        {
            var chamado = await _chamadosRepository.AlterarStatusAsync(
                chamadoId, tenantId, request.NovoStatus, usuarioId, ct);

            return new ChamadoDetailDto(
                chamado.ChamadoId,
                chamado.Numero,
                chamado.Titulo,
                chamado.Descricao,
                chamado.CategoriaId,
                (byte)chamado.StatusId,
                (byte)chamado.PrioridadeId,
                (byte)chamado.CanalId,
                chamado.SolicitanteUsuarioId,
                chamado.ResponsavelUsuarioId,
                chamado.DataCriacao,
                chamado.DataAtualizacao,
                EnumHelperService.GetStatusNome(chamado.StatusId),
                EnumHelperService.GetPrioridadeNome(chamado.PrioridadeId),
                EnumHelperService.GetCanalNome(chamado.CanalId)
            );
        }

        public async Task<ChamadoDetailDto> AdicionarInteracaoIaAsync(
            long chamadoId,
            int tenantId,
            string modelo,
            string mensagem,
            decimal? confianca,
            string? resumoRaciocinio,
            string provedor,
            int? inputTokens,
            int? outputTokens,
            decimal? custoUsd,
            CancellationToken ct)
        {
            var chamado = await _chamadosRepository.AdicionarInteracaoIaAsync(
                chamadoId, tenantId, modelo, mensagem, confianca, resumoRaciocinio,
                provedor, inputTokens, outputTokens, custoUsd, ct);

            return new ChamadoDetailDto(
                chamado.ChamadoId,
                chamado.Numero,
                chamado.Titulo,
                chamado.Descricao,
                chamado.CategoriaId,
                (byte)chamado.StatusId,
                (byte)chamado.PrioridadeId,
                (byte)chamado.CanalId,
                chamado.SolicitanteUsuarioId,
                chamado.ResponsavelUsuarioId,
                chamado.DataCriacao,
                chamado.DataAtualizacao,
                EnumHelperService.GetStatusNome(chamado.StatusId),
                EnumHelperService.GetPrioridadeNome(chamado.PrioridadeId),
                EnumHelperService.GetCanalNome(chamado.CanalId)
            );
        }

        public async Task AdicionarAnexoAsync(
            int tenantId,
            long chamadoId,
            int usuarioId,
            string nomeArquivo,
            string contentType,
            byte[] bytes,
            CancellationToken ct)
        {

            var maxFileSizeBytes = long.Parse(_configuration["FileUpload:MaxFileSizeBytes"] ?? "10485760"); // 10MB padrão
            var allowedExtensions = (_configuration["FileUpload:AllowedExtensions"] ?? ".pdf,.doc,.docx,.jpg,.jpeg,.png,.gif,.txt")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim().ToLowerInvariant())
                .ToHashSet();

            if (bytes.Length > maxFileSizeBytes)
            {
                throw new ArgumentException($"Arquivo muito grande. Tamanho máximo permitido: {maxFileSizeBytes / 1024 / 1024}MB");
            }

            var extension = Path.GetExtension(nomeArquivo)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
            {
                throw new ArgumentException($"Tipo de arquivo não permitido. Extensões permitidas: {string.Join(", ", allowedExtensions)}");
            }

            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(bytes);

            var anexo = new ChamadoAnexo
            {
                ChamadoId = chamadoId,
                TenantId = tenantId,
                NomeArquivo = nomeArquivo,
                ContentType = contentType,
                TamanhoBytes = bytes.Length,
                Conteudo = bytes,
                HashConteudo = hash,
                DataCriacao = DateTime.UtcNow,
                Excluido = false
            };

            await _anexosRepository.AdicionarAsync(anexo, ct);
        }

        public async Task<ChamadoAnexo?> ObterAnexoAsync(long anexoId, int tenantId, CancellationToken ct)
        {
            return await _anexosRepository.ObterPorIdAsync(anexoId, tenantId, ct);
        }

        public async Task<IReadOnlyList<ChamadoAnexo>> ListarAnexosAsync(long chamadoId, CancellationToken ct)
        {
            return await _anexosRepository.ListarPorChamadoAsync((int)chamadoId, ct);
        }

        public async Task<EstatisticasChamadosDto> ObterEstatisticasAsync(
            int tenantId,
            int? solicitanteUsuarioId,
            CancellationToken ct)
        {
            var (total, abertos, emAndamento, resolvidos, cancelados) = 
                await _chamadosRepository.ObterEstatisticasAsync(tenantId, solicitanteUsuarioId, ct);

            var (_, _, _, _, _, porUrgenciaAlta, porUrgenciaMedia, porUrgenciaBaixa) = 
                await ObterEstatisticasPorPrioridadeAsync(tenantId, solicitanteUsuarioId, ct);

            return new EstatisticasChamadosDto(
                total,
                abertos,
                emAndamento,
                resolvidos,
                cancelados,
                porUrgenciaAlta,
                porUrgenciaMedia,
                porUrgenciaBaixa
            );
        }

        private async Task<(int, int, int, int, int, int, int, int)> ObterEstatisticasPorPrioridadeAsync(
            int tenantId,
            int? solicitanteUsuarioId,
            CancellationToken ct)
        {

            var (chamados, _) = await _chamadosRepository.ListaAsync(
                tenantId, 
                null, 
                null, 
                solicitanteUsuarioId, 
                1, 
                int.MaxValue, 
                ct);

            var porUrgenciaAlta = chamados.Count(c => (byte)c.PrioridadeId == 4);
            var porUrgenciaMedia = chamados.Count(c => (byte)c.PrioridadeId == 2 || (byte)c.PrioridadeId == 3);
            var porUrgenciaBaixa = chamados.Count(c => (byte)c.PrioridadeId == 1);

            return (0, 0, 0, 0, 0, porUrgenciaAlta, porUrgenciaMedia, porUrgenciaBaixa);
        }

        public async Task RegistrarFeedbackAsync(
            long chamadoId,
            int tenantId,
            int usuarioId,
            IAFeedbackScoreDto score,
            string? comentario,
            CancellationToken ct)
        {

            var chamado = await _chamadosRepository.ObterAsync(chamadoId, ct);
            if (chamado == null || chamado.TenantId != tenantId)
                throw new ArgumentException("Chamado não encontrado.");

            var scoreEnum = (IAFeedbackScore)(byte)score;

            await _feedbackRepository.AdicionarAsync(
                tenantId,
                chamadoId,
                null, // interacaoId
                usuarioId,
                scoreEnum,
                comentario,
                ct);
        }



        private static DateTime CalcularSLAEstimado(byte prioridadeId)
        {
            var agora = DateTime.UtcNow;
            return prioridadeId switch
            {
                1 => agora.AddHours(72),  // Baixa: 72 horas (3 dias)
                2 => agora.AddHours(48),  // Média: 48 horas (2 dias)
                3 => agora.AddHours(24),   // Alta: 24 horas (1 dia)
                4 => agora.AddHours(4),   // Urgente: 4 horas
                _ => agora.AddHours(48)   // Padrão: 48 horas
            };
        }
    }
}