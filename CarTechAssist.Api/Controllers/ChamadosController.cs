using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using CarTechAssist.Application.Services;
using CarTechAssist.Contracts.Common;
using CarTechAssist.Contracts.Enums;
using CarTechAssist.Contracts.Feedback;
using CarTechAssist.Contracts.Tickets;

namespace CarTechAssist.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
            if (!string.IsNullOrEmpty(tenantIdHeader) && int.TryParse(tenantIdHeader, out var tenantId) && tenantId > 0)
            {

                if (User?.Identity?.IsAuthenticated == true)
                {
                    var jwtTenantId = User.FindFirst("TenantId")?.Value;
                    if (!string.IsNullOrEmpty(jwtTenantId) && int.TryParse(jwtTenantId, out var jwtTenant))
                    {
                        if (jwtTenant != tenantId)
                        {
                            throw new UnauthorizedAccessException("TenantId do header não corresponde ao token JWT.");
                        }
                    }
                }
                return tenantId;
            }

            if (User?.Identity?.IsAuthenticated == true)
            {
                var jwtTenantId = User.FindFirst("TenantId")?.Value;
                if (!string.IsNullOrEmpty(jwtTenantId) && int.TryParse(jwtTenantId, out var jwtTenant) && jwtTenant > 0)
                {
                    return jwtTenant;
                }
            }

            throw new UnauthorizedAccessException("TenantId não encontrado no header X-Tenant-Id ou no token JWT. Faça login primeiro ou forneça o header X-Tenant-Id.");
        }




        private int GetUsuarioId()
        {

            var usuarioIdHeader = Request.Headers["X-Usuario-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(usuarioIdHeader) && int.TryParse(usuarioIdHeader, out var usuarioId) && usuarioId > 0)
            {

                if (User?.Identity?.IsAuthenticated == true)
                {
                    var jwtUsuarioId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    if (!string.IsNullOrEmpty(jwtUsuarioId) && int.TryParse(jwtUsuarioId, out var jwtUsuario))
                    {
                        if (jwtUsuario != usuarioId)
                        {
                            throw new UnauthorizedAccessException("UsuarioId do header não corresponde ao token JWT.");
                        }
                    }
                }
                return usuarioId;
            }

            if (User?.Identity?.IsAuthenticated == true)
            {
                var jwtUsuarioId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(jwtUsuarioId) && int.TryParse(jwtUsuarioId, out var jwtUsuario) && jwtUsuario > 0)
                {
                    return jwtUsuario;
                }
            }

            throw new UnauthorizedAccessException("UsuarioId não encontrado no header X-Usuario-Id ou no token JWT. Faça login primeiro ou forneça o header X-Usuario-Id.");
        }

        [HttpGet]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<ActionResult<PagedResult<TicketView>>> Listar(
            [FromQuery] byte? statusId,
            [FromQuery] int? responsavelUsuarioId,
            [FromQuery] int? solicitanteUsuarioId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {

            const int maxPageSize = 100;
            if (pageSize > maxPageSize)
                pageSize = maxPageSize;
            if (pageSize < 1)
                pageSize = 20;
            if (page < 1)
                page = 1;

            try
            {
                var tenantId = GetTenantId();

                var tipoUsuarioIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                if (byte.TryParse(tipoUsuarioIdStr, out var tipoUsuarioId) && tipoUsuarioId == 1)
                {

                    solicitanteUsuarioId = GetUsuarioId();
                }

                var result = await _chamadosService.ListarAsync(
                    tenantId, statusId, responsavelUsuarioId, solicitanteUsuarioId, page, pageSize, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {

                var logger = HttpContext.RequestServices.GetRequiredService<ILogger<ChamadosController>>();
                logger.LogError(ex, "Erro ao listar chamados. TenantId: {TenantId}, StatusId: {StatusId}, SolicitanteUsuarioId: {SolicitanteUsuarioId}",
                    GetTenantId(), statusId, solicitanteUsuarioId);
                
                return StatusCode(500, new { 
                    error = "Erro interno ao listar chamados", 
                    message = ex.Message 
                });
            }
        }

        [HttpGet("{id:long}")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<ActionResult<ChamadoDetailDto>> Obter(long id, CancellationToken ct = default)
        {
            try
            {
                var tipoUsuarioIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                byte? tipoUsuarioId = byte.TryParse(tipoUsuarioIdStr, out var tipo) ? tipo : null;

                var result = await _chamadosService.ObterAsync(
                    id, 
                    GetTenantId(), 
                    GetUsuarioId(), 
                    tipoUsuarioId, 
                    ct);
                
                if (result == null) return NotFound();
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
        }

      
        [HttpPost]
        [Microsoft.AspNetCore.Authorization.Authorize]
        [ProducesResponseType(typeof(ChamadoDetailDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ChamadoDetailDto>> Criar(
            [FromBody] CriarChamadoRequest request,
            CancellationToken ct = default)
        {
            try
            {

                var usuarioIdAutenticado = GetUsuarioId();
                var tenantId = GetTenantId();

                var requestCorrigido = new CriarChamadoRequest(
                    Titulo: request.Titulo,
                    Descricao: request.Descricao,
                    CategoriaId: request.CategoriaId,
                    PrioridadeId: request.PrioridadeId,
                    CanalId: request.CanalId,
                    SolicitanteUsuarioId: usuarioIdAutenticado, // SEMPRE usar o usuário autenticado
                    ResponsavelUsuarioId: request.ResponsavelUsuarioId, // Manter responsável se fornecido
                    SLA_EstimadoFim: request.SLA_EstimadoFim // Manter SLA se fornecido, senão será calculado
                );
                
                var result = await _chamadosService.CriarAsync(tenantId, requestCorrigido, ct);
                return CreatedAtAction(nameof(Obter), new { id = result.ChamadoId }, result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message, error = "Erro de validação" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message, error = "Erro de permissão" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message, error = "Erro de operação" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message, error = "Erro interno do servidor" });
            }
        }

        [HttpGet("{id:long}/interacoes")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<ActionResult<IReadOnlyList<InteracaoDto>>> ListarInteracoes(
            long id,
            CancellationToken ct = default)
        {
            var result = await _chamadosService.ListarInteracoesAsync(id, GetTenantId(), ct);
            return Ok(result);
        }

        [HttpPost("{id:long}/interacoes")]
        [Microsoft.AspNetCore.Authorization.Authorize]
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
            if (arquivo == null || arquivo.Length == 0)
                return BadRequest("Arquivo não fornecido.");

            try
            {
                using var stream = new MemoryStream();
                await arquivo.CopyToAsync(stream, ct);
                var bytes = stream.ToArray();

                await _chamadosService.AdicionarAnexoAsync(
                    GetTenantId(), id, GetUsuarioId(), arquivo.FileName, arquivo.ContentType, bytes, ct);

                return Ok(new { message = "Arquivo enviado com sucesso." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("{id:long}/anexos")]
        public async Task<ActionResult<IReadOnlyList<AnexoDto>>> ListarAnexos(
            long id,
            CancellationToken ct = default)
        {
            var anexos = await _chamadosService.ListarAnexosAsync(id, ct);
            var anexosDto = anexos.Select(a => new AnexoDto(
                a.AnexoId,
                a.ChamadoId,
                a.InteracaoId,
                a.NomeArquivo,
                a.ContentType,
                a.TamanhoBytes,
                a.UrlExterna,
                a.DataCriacao
            )).ToList();

            return Ok(anexosDto);
        }

        [HttpGet("{id:long}/anexos/{anexoId:long}")]
        public async Task<IActionResult> DownloadAnexo(
            long id,
            long anexoId,
            CancellationToken ct = default)
        {
            var anexo = await _chamadosService.ObterAnexoAsync(anexoId, GetTenantId(), ct);
            if (anexo == null || anexo.ChamadoId != id)
                return NotFound("Anexo não encontrado.");

            if (anexo.Conteudo == null || anexo.Conteudo.Length == 0)
                return NotFound("Conteúdo do anexo não disponível.");

            return File(anexo.Conteudo, anexo.ContentType ?? "application/octet-stream", anexo.NomeArquivo);
        }

        [HttpPatch("{id:long}/status")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> AlterarStatus(
            long id,
            [FromBody] AlterarStatusRequest request,
            CancellationToken ct = default)
        {
            try
            {

                var tipoUsuarioIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                if (!byte.TryParse(tipoUsuarioIdStr, out var tipoUsuarioId) || (tipoUsuarioId != 2 && tipoUsuarioId != 3))
                {
                    return StatusCode(403, new { message = "Apenas técnicos podem alterar o status do chamado." });
                }

                var result = await _chamadosService.AlterarStatusAsync(
                    GetTenantId(), id, GetUsuarioId(), request, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                var logger = HttpContext.RequestServices.GetRequiredService<ILogger<ChamadosController>>();
                logger.LogError(ex, "Erro ao alterar status do chamado {ChamadoId}", id);
                return StatusCode(500, new { message = "Erro ao alterar status do chamado.", error = ex.Message });
            }
        }

        [HttpGet("estatisticas")]
        public async Task<ActionResult<EstatisticasChamadosDto>> ObterEstatisticas(CancellationToken ct = default)
        {

            var tipoUsuarioIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            int? solicitanteUsuarioId = null;
            
            if (byte.TryParse(tipoUsuarioIdStr, out var tipoUsuarioId) && tipoUsuarioId == 1)
            {
                solicitanteUsuarioId = GetUsuarioId();
            }

            var estatisticas = await _chamadosService.ObterEstatisticasAsync(
                GetTenantId(), solicitanteUsuarioId, ct);
            
            return Ok(estatisticas);
        }

        [HttpPost("{id:long}/feedback")]
        public async Task<IActionResult> EnviarFeedback(
            long id, 
            [FromBody] EnviarFeedbackRequest request,
            CancellationToken ct = default)
        {
            try
            {
                await _chamadosService.RegistrarFeedbackAsync(
                    id, 
                    GetTenantId(), 
                    GetUsuarioId(), 
                    request.Score, 
                    request.Comentario, 
                    ct);
                
                return Ok(new { message = "Feedback registrado com sucesso." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
