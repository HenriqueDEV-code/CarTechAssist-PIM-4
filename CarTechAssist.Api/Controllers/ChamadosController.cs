using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CarTechAssist.Api.Attributes;
using CarTechAssist.Application.Services;
using CarTechAssist.Contracts.Common;
using CarTechAssist.Contracts.Enums;
using CarTechAssist.Contracts.Feedback;
using CarTechAssist.Contracts.Tickets;
using CarTechAssist.Domain.Enums;
using System.IO;

namespace CarTechAssist.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Requer autenticação para todos os endpoints
    public class ChamadosController : ControllerBase
    {
        private readonly ChamadosService _chamadosService;

        public ChamadosController(ChamadosService chamadosService)
        {
            _chamadosService = chamadosService;
        }

        private int GetTenantId()
        {
            var tenantIdHeader = Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (string.IsNullOrEmpty(tenantIdHeader) || !int.TryParse(tenantIdHeader, out var tenantId))
                throw new UnauthorizedAccessException("TenantId não encontrado ou inválido no header X-Tenant-Id.");
            return tenantId;
        }

        private int GetUsuarioId()
        {
            var usuarioIdHeader = Request.Headers["X-Usuario-Id"].FirstOrDefault();
            if (string.IsNullOrEmpty(usuarioIdHeader) || !int.TryParse(usuarioIdHeader, out var usuarioId))
                throw new UnauthorizedAccessException("UsuarioId não encontrado ou inválido no header X-Usuario-Id.");
            return usuarioId;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<TicketView>>> Listar(
            [FromQuery] byte? statusId,
            [FromQuery] int? responsavelUsuarioId,
            [FromQuery] int? solicitanteUsuarioId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            // Validação de parâmetros de paginação
            if (page < 1)
                return BadRequest("O parâmetro 'page' deve ser maior ou igual a 1.");
            
            if (pageSize < 1 || pageSize > 100)
                return BadRequest("O parâmetro 'pageSize' deve estar entre 1 e 100.");

            // CRÍTICO: Obter tipo de usuário do JWT para validação de permissões
            var tipoUsuarioId = GetTipoUsuarioId();
            
            // Se for Cliente, forçar filtro por solicitante (ele mesmo)
            if (tipoUsuarioId == (byte)Domain.Enums.TipoUsuarios.Cliente)
            {
                solicitanteUsuarioId = GetUsuarioId();
            }

            var result = await _chamadosService.ListarAsync(
                GetTenantId(), statusId, responsavelUsuarioId, solicitanteUsuarioId, page, pageSize, tipoUsuarioId, ct);
            return Ok(result);
        }

        private byte GetTipoUsuarioId()
        {
            var roleClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Role);
            if (roleClaim != null && byte.TryParse(roleClaim.Value, out var tipoUsuarioId))
            {
                return tipoUsuarioId;
            }
            // Se não encontrar, assumir que não é cliente (fallback para Admin/Técnico)
            return (byte)Domain.Enums.TipoUsuarios.Administrador;
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<ChamadoDetailDto>> Obter(long id, CancellationToken ct = default)
        {
            var result = await _chamadosService.ObterAsync(GetTenantId(), id, ct);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost]
        // Todos os usuários autenticados podem criar chamados (Cliente, Técnico, Admin)
        public async Task<ActionResult<ChamadoDetailDto>> Criar(
            [FromBody] CriarChamadoRequest request,
            CancellationToken ct = default)
        {
            var result = await _chamadosService.CriarAsync(GetTenantId(), request, ct);
            return CreatedAtAction(nameof(Obter), new { id = result.ChamadoId }, result);
        }

        [HttpPost("{id:long}/interacoes")]
        public async Task<IActionResult> AdicionarInteracao(
            long id,
            [FromBody] AdicionarInteracaoRequest request,
            CancellationToken ct = default)
        {
            var result = await _chamadosService.AdicionarInteracaoAsync(
                GetTenantId(), id, GetUsuarioId(), request, ct);
            return Ok(result);
        }

        [HttpPost("{id:long}/anexos")]
        public async Task<IActionResult> UploadAnexo(
            long id,
            IFormFile arquivo,
            CancellationToken ct = default)
        {
            // Validações de arquivo
            if (arquivo == null || arquivo.Length == 0)
                return BadRequest("Arquivo não fornecido.");

            // Tamanho máximo: 10MB
            const long maxFileSize = 10 * 1024 * 1024; // 10MB
            if (arquivo.Length > maxFileSize)
                return BadRequest($"Arquivo excede o tamanho máximo permitido de {maxFileSize / 1024 / 1024}MB.");

            // Tipos de arquivo permitidos
            var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png", ".gif", ".txt", ".zip", ".rar" };
            var fileExtension = Path.GetExtension(arquivo.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(fileExtension) || !allowedExtensions.Contains(fileExtension))
                return BadRequest($"Tipo de arquivo não permitido. Extensões permitidas: {string.Join(", ", allowedExtensions)}");

            using var stream = new MemoryStream();
            await arquivo.CopyToAsync(stream, ct);
            var bytes = stream.ToArray();

            await _chamadosService.AdicionarAnexoAsync(
                GetTenantId(), id, GetUsuarioId(), arquivo.FileName, arquivo.ContentType, bytes, ct);

            return Ok(new { message = "Anexo adicionado com sucesso.", fileName = arquivo.FileName });
        }

        [HttpPatch("{id:long}/status")]
        [AuthorizeRoles((byte)TipoUsuarios.Administrador, (byte)TipoUsuarios.Tecnico)] // Apenas Técnico e Admin podem alterar status
        public async Task<IActionResult> AlterarStatus(
            long id,
            [FromBody] AlterarStatusRequest request,
            CancellationToken ct = default)
        {
            var result = await _chamadosService.AlterarStatusAsync(
                GetTenantId(), id, GetUsuarioId(), request, ct);
            return Ok(result);
        }

        [HttpPost("{id:long}/feedback")]
        public async Task<ActionResult<ChamadoDetailDto>> EnviarFeedback(
            long id,
            [FromBody] EnviarFeedbackRequest request,
            CancellationToken ct = default)
        {
            try
            {
                // Criar request completo com dados do contexto
                var feedbackRequest = new FeedbackRequest(
                    id, // ChamadoId da rota
                    GetTenantId(), // TenantId do header
                    request.Score,
                    request.Comentario
                );
                
                var result = await _chamadosService.AdicionarFeedbackAsync(
                    GetTenantId(), id, GetUsuarioId(), feedbackRequest, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpGet("{id:long}/interacoes")]
        public async Task<ActionResult<IReadOnlyList<InteracaoDto>>> ListarInteracoes(
            long id,
            CancellationToken ct = default)
        {
            try
            {
                var result = await _chamadosService.ListarInteracoesAsync(GetTenantId(), id, ct);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpGet("{id:long}/anexos")]
        public async Task<ActionResult<IReadOnlyList<AnexoDto>>> ListarAnexos(
            long id,
            CancellationToken ct = default)
        {
            try
            {
                var result = await _chamadosService.ListarAnexosAsync(GetTenantId(), id, ct);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpGet("{id:long}/anexos/{anexoId:long}/download")]
        public async Task<IActionResult> DownloadAnexo(
            long id,
            long anexoId,
            CancellationToken ct = default)
        {
            try
            {
                var (conteudo, nomeArquivo, contentType) = await _chamadosService.DownloadAnexoAsync(
                    GetTenantId(), id, anexoId, ct);
                
                return File(conteudo, contentType, nomeArquivo);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpPut("{id:long}")]
        [AuthorizeRoles((byte)Domain.Enums.TipoUsuarios.Administrador, (byte)Domain.Enums.TipoUsuarios.Tecnico)] // Apenas Admin e Técnico podem atualizar
        public async Task<ActionResult<ChamadoDetailDto>> Atualizar(
            long id,
            [FromBody] AtualizarChamadoRequest request,
            CancellationToken ct = default)
        {
            try
            {
                var result = await _chamadosService.AtualizarAsync(
                    GetTenantId(), id, GetUsuarioId(), request, ct);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPatch("{id:long}/responsavel")]
        [AuthorizeRoles((byte)Domain.Enums.TipoUsuarios.Administrador, (byte)Domain.Enums.TipoUsuarios.Tecnico)] // Apenas Admin e Técnico podem atribuir
        public async Task<ActionResult<ChamadoDetailDto>> AtribuirResponsavel(
            long id,
            [FromBody] AtribuirResponsavelRequest request,
            CancellationToken ct = default)
        {
            try
            {
                var result = await _chamadosService.AtribuirResponsavelAsync(
                    GetTenantId(), id, GetUsuarioId(), request, ct);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{id:long}/historico-status")]
        public async Task<ActionResult<IReadOnlyList<StatusHistoricoDto>>> ListarHistoricoStatus(
            long id,
            CancellationToken ct = default)
        {
            try
            {
                var result = await _chamadosService.ListarHistoricoStatusAsync(GetTenantId(), id, ct);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpDelete("{id:long}")]
        [AuthorizeRoles((byte)Domain.Enums.TipoUsuarios.Administrador)] // Apenas Admin pode deletar
        public async Task<IActionResult> Deletar(
            long id,
            [FromBody] DeletarChamadoRequest request,
            CancellationToken ct = default)
        {
            try
            {
                await _chamadosService.DeletarAsync(GetTenantId(), id, GetUsuarioId(), request.Motivo, ct);
                return Ok(new { message = "Chamado deletado com sucesso." });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
