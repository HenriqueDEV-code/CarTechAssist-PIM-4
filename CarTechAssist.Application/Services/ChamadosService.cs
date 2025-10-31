using CarTechAssist.Contracts.Common;
using CarTechAssist.Contracts.Enums;
using CarTechAssist.Contracts.Feedback;
using CarTechAssist.Contracts.Tickets;
using CarTechAssist.Domain.Entities;
using CarTechAssist.Domain.Enums;
using CarTechAssist.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CarTechAssist.Application.Services
{
    public class ChamadosService
    {
        private readonly IChamadosRepository _chamadosRepository;
        private readonly ILogger<ChamadosService> _logger;
        private readonly IUsuariosRepository _usuariosRepository;
        private readonly ICategoriasRepository _categoriasRepository;

        public ChamadosService(
            IChamadosRepository chamadosRepository, 
            IUsuariosRepository usuariosRepository,
            ICategoriasRepository categoriasRepository,
            ILogger<ChamadosService> logger)
        {
            _chamadosRepository = chamadosRepository;
            _usuariosRepository = usuariosRepository;
            _categoriasRepository = categoriasRepository;
            _logger = logger;
        }

        /// <summary>
        /// Valida se a transição de status é permitida pelas regras de negócio
        /// </summary>
        private void ValidarTransicaoStatus(StatusChamado statusAtual, StatusChamado novoStatus)
        {
            var transicoesPermitidas = statusAtual switch
            {
                StatusChamado.Aberto => new[] 
                { 
                    StatusChamado.EmAndamento, 
                    StatusChamado.Pendente, 
                    StatusChamado.Cancelado 
                },
                StatusChamado.EmAndamento => new[] 
                { 
                    StatusChamado.Pendente, 
                    StatusChamado.Resolvido, 
                    StatusChamado.Cancelado 
                },
                StatusChamado.Pendente => new[] 
                { 
                    StatusChamado.EmAndamento, 
                    StatusChamado.Cancelado 
                },
                StatusChamado.Resolvido => new[] 
                { 
                    StatusChamado.Fechado 
                },
                StatusChamado.Fechado => Array.Empty<StatusChamado>(), // Não pode mudar
                StatusChamado.Cancelado => Array.Empty<StatusChamado>(), // Não pode mudar
                _ => Array.Empty<StatusChamado>()
            };

            if (!transicoesPermitidas.Contains(novoStatus))
            {
                throw new InvalidOperationException(
                    $"Transição de status inválida: não é possível alterar de '{statusAtual}' para '{novoStatus}'. " +
                    $"Transições permitidas: {string.Join(", ", transicoesPermitidas)}");
            }
        }

        public async Task<PagedResult<TicketView>> ListarAsync(
            int tenantId,
            byte? statusId,
            int? responsavelUsuarioId,
            int? solicitanteUsuarioId, // Novo filtro
            int page,
            int pageSize,
            byte? tipoUsuarioId, // Para validar permissões
            CancellationToken ct)
        {
            // CRÍTICO: Cliente só pode ver seus próprios chamados
            // Nota: O filtro por solicitanteUsuarioId é passado pelo controller quando é cliente
            if (tipoUsuarioId == (byte)TipoUsuarios.Cliente)
            {
                // Cliente não pode filtrar por responsável
                responsavelUsuarioId = null;
            }

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

        public async Task<ChamadoDetailDto?> ObterAsync(int tenantId, long chamadoId, CancellationToken ct)
        {
            _logger.LogInformation("Obtendo chamado {ChamadoId} para tenant {TenantId}", chamadoId, tenantId);
            var chamado = await _chamadosRepository.ObterAsync(chamadoId, ct);
            if (chamado == null)
            {
                _logger.LogWarning("Chamado {ChamadoId} não encontrado", chamadoId);
                return null;
            }

            // Validação de segurança: verificar se o chamado pertence ao tenant
            if (chamado.TenantId != tenantId)
            {
                _logger.LogWarning("Tentativa de acesso a chamado de outro tenant. ChamadoId: {ChamadoId}, Tenant esperado: {TenantId}, Tenant do chamado: {ChamadoTenantId}",
                    chamadoId, tenantId, chamado.TenantId);
                return null;
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
            _logger.LogInformation("Criando novo chamado para tenant {TenantId}: {Titulo}", tenantId, request.Titulo);

            // CRÍTICO: Validações de integridade referencial
            // Validar categoria
            if (request.CategoriaId.HasValue)
            {
                var categorias = await _categoriasRepository.ListarAtivasAsync(tenantId, ct);
                var categoria = categorias.FirstOrDefault(c => c.CategoriaId == request.CategoriaId.Value);
                if (categoria == null)
                {
                    throw new InvalidOperationException($"Categoria {request.CategoriaId.Value} não encontrada ou inativa para este tenant.");
                }
            }

            // Validar solicitante
            var solicitante = await _usuariosRepository.ObterPorIdAsync(request.SolicitanteUsuarioId, ct);
            if (solicitante == null || solicitante.TenantId != tenantId || !solicitante.Ativo || solicitante.Excluido)
            {
                throw new InvalidOperationException("Solicitante inválido, inativo ou não pertence ao tenant.");
            }

            // Validar responsável (se informado)
            if (request.ResponsavelUsuarioId.HasValue)
            {
                var responsavel = await _usuariosRepository.ObterPorIdAsync(request.ResponsavelUsuarioId.Value, ct);
                if (responsavel == null || responsavel.TenantId != tenantId || !responsavel.Ativo || responsavel.Excluido)
                {
                    throw new InvalidOperationException("Responsável inválido, inativo ou não pertence ao tenant.");
                }
                
                // Responsável deve ser Técnico ou Admin
                if (responsavel.TipoUsuarioId != TipoUsuarios.Tecnico && 
                    responsavel.TipoUsuarioId != TipoUsuarios.Administrador)
                {
                    throw new InvalidOperationException("Responsável deve ser Técnico ou Administrador.");
                }
            }

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

            // Validar que status inicial é Aberto
            if (chamado.StatusId != StatusChamado.Aberto)
            {
                _logger.LogWarning("Chamado criado com status diferente de Aberto: {StatusId}", chamado.StatusId);
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
            // CRÍTICO: Validar transição de status
            var chamadoAtual = await _chamadosRepository.ObterAsync(chamadoId, ct);
            if (chamadoAtual == null || chamadoAtual.TenantId != tenantId)
            {
                throw new UnauthorizedAccessException("Chamado não encontrado ou não pertence ao tenant atual.");
            }

            var novoStatus = (StatusChamado)request.NovoStatus;
            ValidarTransicaoStatus(chamadoAtual.StatusId, novoStatus);

            var chamado = await _chamadosRepository.AlterarStatusAsync(
                chamadoId, tenantId, request.NovoStatus, usuarioId, ct);

            // CRÍTICO: Atualizar datas automaticamente se a stored procedure não atualizou
            // Isso garante que as datas sejam atualizadas mesmo que a SP não faça
            bool precisaAtualizar = false;
            DateTime? dataResolvido = null;
            DateTime? dataFechado = null;

            if (novoStatus == StatusChamado.Resolvido && chamado.DataResolvido == null)
            {
                dataResolvido = DateTime.UtcNow;
                precisaAtualizar = true;
            }

            if (novoStatus == StatusChamado.Fechado && chamado.DataFechado == null)
            {
                dataFechado = DateTime.UtcNow;
                precisaAtualizar = true;
            }

            // Se precisa atualizar, fazer UPDATE adicional
            if (precisaAtualizar)
            {
                chamado = await _chamadosRepository.AtualizarDatasStatusAsync(
                    chamadoId, tenantId, dataResolvido, dataFechado, ct);
            }

            _logger.LogInformation(
                "Status do chamado {ChamadoId} alterado de {StatusAntigo} para {StatusNovo} pelo usuário {UsuarioId}",
                chamadoId, chamadoAtual.StatusId, novoStatus, usuarioId);

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
            await _chamadosRepository.AdicionarAnexoAsync(
                chamadoId, tenantId, nomeArquivo, contentType, bytes, usuarioId, ct);
        }

        public async Task<ChamadoDetailDto> AdicionarFeedbackAsync(
            int tenantId,
            long chamadoId,
            int? usuarioId,
            FeedbackRequest request,
            CancellationToken ct)
        {
            _logger.LogInformation("Adicionando feedback ao chamado {ChamadoId} para tenant {TenantId}", chamadoId, tenantId);
            
            // Validar que o chamado existe e pertence ao tenant
            var chamado = await _chamadosRepository.ObterAsync(chamadoId, ct);
            if (chamado == null)
            {
                _logger.LogWarning("Tentativa de adicionar feedback em chamado inexistente: {ChamadoId}", chamadoId);
                throw new InvalidOperationException($"Chamado {chamadoId} não encontrado.");
            }

            if (chamado.TenantId != tenantId)
            {
                _logger.LogWarning("Tentativa de adicionar feedback em chamado de outro tenant. Chamado: {ChamadoId}, Tenant esperado: {TenantId}, Tenant do chamado: {ChamadoTenantId}", 
                    chamadoId, tenantId, chamado.TenantId);
                throw new UnauthorizedAccessException("Chamado não pertence ao tenant atual.");
            }

            // Converter o score do DTO para byte
            byte score = (byte)request.Score;

            chamado = await _chamadosRepository.AdicionarFeedbackAsync(
                chamadoId, tenantId, usuarioId, score, request.Comentario, ct);

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
            int tenantId,
            long chamadoId,
            CancellationToken ct)
        {
            _logger.LogInformation("Listando interações do chamado {ChamadoId} para tenant {TenantId}", chamadoId, tenantId);

            // Validar que o chamado existe e pertence ao tenant
            var chamado = await _chamadosRepository.ObterAsync(chamadoId, ct);
            if (chamado == null || chamado.TenantId != tenantId)
            {
                throw new UnauthorizedAccessException("Chamado não encontrado ou não pertence ao tenant atual.");
            }

            // Usar método otimizado com JOIN para evitar N+1 query
            var interacoesComAutor = await _chamadosRepository.ListarInteracoesComAutorAsync(chamadoId, tenantId, ct);
            
            var dtos = interacoesComAutor.Select(item => new InteracaoDto(
                item.Interacao.InteracaoId,
                item.Interacao.ChamadoId,
                item.Interacao.AutorUsuarioId,
                item.AutorNome,
                (byte)item.Interacao.AutorTipoUsuarioId,
                (byte)item.Interacao.CanalId,
                item.Interacao.Mensagem,
                item.Interacao.Interna,
                item.Interacao.IA_Gerada,
                item.Interacao.IA_Modelo,
                item.Interacao.IA_Confianca,
                item.Interacao.IA_ResumoRaciocinio,
                item.Interacao.DataCriacao
            )).ToList();

            return dtos;
        }

        public async Task<IReadOnlyList<AnexoDto>> ListarAnexosAsync(
            int tenantId,
            long chamadoId,
            CancellationToken ct)
        {
            _logger.LogInformation("Listando anexos do chamado {ChamadoId} para tenant {TenantId}", chamadoId, tenantId);

            // Validar que o chamado existe e pertence ao tenant
            var chamado = await _chamadosRepository.ObterAsync(chamadoId, ct);
            if (chamado == null || chamado.TenantId != tenantId)
            {
                throw new UnauthorizedAccessException("Chamado não encontrado ou não pertence ao tenant atual.");
            }

            var anexos = await _chamadosRepository.ListarAnexosAsync(chamadoId, tenantId, ct);

            return anexos.Select(a => new AnexoDto(
                a.AnexoId,
                a.ChamadoId,
                a.InteracaoId,
                a.NomeArquivo,
                a.ContentType,
                a.TamanhoBytes,
                a.UrlExterna,
                a.DataCriacao
            )).ToList();
        }

        public async Task<(byte[] Conteudo, string NomeArquivo, string ContentType)> DownloadAnexoAsync(
            int tenantId,
            long chamadoId,
            long anexoId,
            CancellationToken ct)
        {
            _logger.LogInformation("Download anexo {AnexoId} do chamado {ChamadoId} para tenant {TenantId}", anexoId, chamadoId, tenantId);

            // Validar que o chamado existe e pertence ao tenant
            var chamado = await _chamadosRepository.ObterAsync(chamadoId, ct);
            if (chamado == null || chamado.TenantId != tenantId)
            {
                throw new UnauthorizedAccessException("Chamado não encontrado ou não pertence ao tenant atual.");
            }

            var anexo = await _chamadosRepository.ObterAnexoAsync(anexoId, tenantId, ct);
            if (anexo == null || anexo.ChamadoId != chamadoId)
            {
                throw new InvalidOperationException("Anexo não encontrado ou não pertence ao chamado.");
            }

            if (anexo.Conteudo == null)
            {
                throw new InvalidOperationException("Conteúdo do anexo não disponível.");
            }

            return (anexo.Conteudo, anexo.NomeArquivo, anexo.ContentType ?? "application/octet-stream");
        }

        public async Task<ChamadoDetailDto> AtualizarAsync(
            int tenantId,
            long chamadoId,
            int usuarioId,
            AtualizarChamadoRequest request,
            CancellationToken ct)
        {
            _logger.LogInformation("Atualizando chamado {ChamadoId} para tenant {TenantId}", chamadoId, tenantId);

            // Validar que o chamado existe e pertence ao tenant
            var chamadoAtual = await _chamadosRepository.ObterAsync(chamadoId, ct);
            if (chamadoAtual == null || chamadoAtual.TenantId != tenantId)
            {
                throw new UnauthorizedAccessException("Chamado não encontrado ou não pertence ao tenant atual.");
            }

            // Validar categoria (se fornecida)
            if (request.CategoriaId.HasValue)
            {
                var categorias = await _categoriasRepository.ListarAtivasAsync(tenantId, ct);
                var categoria = categorias.FirstOrDefault(c => c.CategoriaId == request.CategoriaId.Value);
                if (categoria == null)
                {
                    throw new InvalidOperationException($"Categoria {request.CategoriaId.Value} não encontrada ou inativa para este tenant.");
                }
            }

            var chamado = await _chamadosRepository.AtualizarAsync(
                chamadoId,
                tenantId,
                request.Titulo,
                request.Descricao,
                request.CategoriaId,
                request.PrioridadeId,
                usuarioId,
                request.MotivoAlteracao,
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

        public async Task<ChamadoDetailDto> AtribuirResponsavelAsync(
            int tenantId,
            long chamadoId,
            int usuarioId,
            AtribuirResponsavelRequest request,
            CancellationToken ct)
        {
            _logger.LogInformation("Atribuindo responsável ao chamado {ChamadoId} para tenant {TenantId}", chamadoId, tenantId);

            // Validar que o chamado existe e pertence ao tenant
            var chamadoAtual = await _chamadosRepository.ObterAsync(chamadoId, ct);
            if (chamadoAtual == null || chamadoAtual.TenantId != tenantId)
            {
                throw new UnauthorizedAccessException("Chamado não encontrado ou não pertence ao tenant atual.");
            }

            // Validar responsável (se fornecido)
            if (request.ResponsavelUsuarioId.HasValue)
            {
                var responsavel = await _usuariosRepository.ObterPorIdAsync(request.ResponsavelUsuarioId.Value, ct);
                if (responsavel == null || responsavel.TenantId != tenantId || !responsavel.Ativo || responsavel.Excluido)
                {
                    throw new InvalidOperationException("Responsável inválido, inativo ou não pertence ao tenant.");
                }

                // Responsável deve ser Técnico ou Admin
                if (responsavel.TipoUsuarioId != TipoUsuarios.Tecnico &&
                    responsavel.TipoUsuarioId != TipoUsuarios.Administrador)
                {
                    throw new InvalidOperationException("Responsável deve ser Técnico ou Administrador.");
                }
            }

            var chamado = await _chamadosRepository.AtribuirResponsavelAsync(
                chamadoId,
                tenantId,
                request.ResponsavelUsuarioId,
                usuarioId,
                request.Motivo,
                ct);

            // IMPORTANTE: Calcular SLA quando responsável é atribuído
            // Isso pode ser feito na stored procedure, mas se não for, adicionar aqui

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

        public async Task<IReadOnlyList<StatusHistoricoDto>> ListarHistoricoStatusAsync(
            int tenantId,
            long chamadoId,
            CancellationToken ct)
        {
            _logger.LogInformation("Listando histórico de status do chamado {ChamadoId} para tenant {TenantId}", chamadoId, tenantId);

            // Validar que o chamado existe e pertence ao tenant
            var chamado = await _chamadosRepository.ObterAsync(chamadoId, ct);
            if (chamado == null || chamado.TenantId != tenantId)
            {
                throw new UnauthorizedAccessException("Chamado não encontrado ou não pertence ao tenant atual.");
            }

            var historicos = await _chamadosRepository.ListarHistoricoStatusAsync(chamadoId, tenantId, ct);

            return historicos.Select(h => new StatusHistoricoDto(
                h.Historico.HistoricoId,
                h.Historico.ChamadoId,
                h.Historico.StatusAntigoId?.ToString(),
                h.Historico.StatusNovoId.ToString(),
                h.Historico.AlteradoPorUsuarioId,
                h.AlteradoPorNome,
                h.Historico.Motivo,
                h.Historico.DataAlteracao
            )).ToList();
        }

        public async Task DeletarAsync(
            int tenantId,
            long chamadoId,
            int usuarioId,
            string motivo,
            CancellationToken ct)
        {
            _logger.LogInformation("Deletando chamado {ChamadoId} para tenant {TenantId}", chamadoId, tenantId);

            if (string.IsNullOrWhiteSpace(motivo))
            {
                throw new ArgumentException("Motivo da exclusão é obrigatório.");
            }

            // Validar que o chamado existe e pertence ao tenant
            var chamado = await _chamadosRepository.ObterAsync(chamadoId, ct);
            if (chamado == null || chamado.TenantId != tenantId)
            {
                throw new UnauthorizedAccessException("Chamado não encontrado ou não pertence ao tenant atual.");
            }

            // Validar que chamado fechado não pode ser deletado (apenas cancelado)
            if (chamado.StatusId == StatusChamado.Fechado)
            {
                throw new InvalidOperationException("Chamado fechado não pode ser deletado.");
            }

            await _chamadosRepository.DeletarAsync(chamadoId, tenantId, usuarioId, motivo, ct);

            _logger.LogInformation("Chamado {ChamadoId} deletado com sucesso. Motivo: {Motivo}", chamadoId, motivo);
        }
    }
}