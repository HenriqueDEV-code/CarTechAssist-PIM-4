using System.IO;
using CarTechAssist.Contracts.Common;
using CarTechAssist.Contracts.Enums;
using CarTechAssist.Contracts.Tickets;
using CarTechAssist.Domain.Entities;
using CarTechAssist.Domain.Enums;
using CarTechAssist.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace CarTechAssist.Application.Services
{
    public class ChamadosService
    {
        private readonly IChamadosRepository _chamadosRepository;
        private readonly IAnexosReposity _anexosRepository;
        private readonly IFeedbackRepository _feedbackRepository;
        private readonly IConfiguration _configuration;

        public ChamadosService(
            IChamadosRepository chamadosRepository,
            IAnexosReposity anexosRepository,
            IFeedbackRepository feedbackRepository,
            IConfiguration configuration)
        {
            _chamadosRepository = chamadosRepository;
            _anexosRepository = anexosRepository;
            _feedbackRepository = feedbackRepository;
            _configuration = configuration;
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
                c.StatusId.ToString(),
                c.PrioridadeId.ToString(),
                c.IA_FeedbackScore.HasValue ? (IAFeedbackScoreDto)(byte)c.IA_FeedbackScore.Value : null,
                c.DataCriacao
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

            // Validar tenant
            if (chamado.TenantId != tenantId)
                throw new UnauthorizedAccessException("Chamado não pertence ao tenant informado.");

            // Validar permissões: Cliente só pode ver seus próprios chamados
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
                chamado.DataAtualizacao
            );
        }

        public async Task<ChamadoDetailDto> CriarAsync(
            int tenantId,
            CriarChamadoRequest request,
            CancellationToken ct)
        {
            var chamado = await _chamadosRepository.CriarAsync(
                tenantId,
                request.Titulo,
                request.Descricao,
                request.CategoriaId,
                request.PrioridadeId,
                request.CanalId,
                request.SolicitanteUsuarioId,
                request.ResponsavelUsuarioId,
                ct);

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
                chamado.DataAtualizacao
            );
        }

        public async Task<IReadOnlyList<InteracaoDto>> ListarInteracoesAsync(
            long chamadoId,
            int tenantId,
            CancellationToken ct)
        {
            var interacoes = await _chamadosRepository.ListarInteracoesAsync(chamadoId, tenantId, ct);
            
            // Retornar interações com nome do tipo de usuário como identificação
            // O nome completo do autor será buscado no frontend se necessário
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
            var chamado = await _chamadosRepository.AdicionarInteracaoAsync(
                chamadoId, tenantId, usuarioId, request.Mensagem, ct);

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
                chamado.DataAtualizacao
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
                chamado.DataAtualizacao
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
                chamado.DataAtualizacao
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
            // Validações de arquivo
            var maxFileSizeBytes = long.Parse(_configuration["FileUpload:MaxFileSizeBytes"] ?? "10485760"); // 10MB padrão
            var allowedExtensions = (_configuration["FileUpload:AllowedExtensions"] ?? ".pdf,.doc,.docx,.jpg,.jpeg,.png,.gif,.txt")
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(e => e.Trim().ToLowerInvariant())
                .ToHashSet();

            // Validar tamanho
            if (bytes.Length > maxFileSizeBytes)
            {
                throw new ArgumentException($"Arquivo muito grande. Tamanho máximo permitido: {maxFileSizeBytes / 1024 / 1024}MB");
            }

            // Validar extensão
            var extension = Path.GetExtension(nomeArquivo)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
            {
                throw new ArgumentException($"Tipo de arquivo não permitido. Extensões permitidas: {string.Join(", ", allowedExtensions)}");
            }

            // Calcular hash do conteúdo
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

            // Obter estatísticas por prioridade
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
            // Buscar todos os chamados para calcular por prioridade
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
            // Validar chamado existe
            var chamado = await _chamadosRepository.ObterAsync(chamadoId, ct);
            if (chamado == null || chamado.TenantId != tenantId)
                throw new ArgumentException("Chamado não encontrado.");

            // Converter DTO para enum domain
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
    }
}