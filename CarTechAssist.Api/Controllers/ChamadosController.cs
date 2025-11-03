using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

        private int GetTenantId() => int.Parse(Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "1");
        private int GetUsuarioId() => int.Parse(Request.Headers["X-Usuario-Id"].FirstOrDefault() ?? "1");

        [HttpGet]
        public async Task<ActionResult<PagedResult<TicketView>>> Listar(
            [FromQuery] byte? statusId,
            [FromQuery] int? responsavelUsuarioId,
            [FromQuery] int? solicitanteUsuarioId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
        {
            // Se for Cliente (TipoUsuarioId = 1), filtrar apenas seus próprios chamados
            var tipoUsuarioIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            if (byte.TryParse(tipoUsuarioIdStr, out var tipoUsuarioId) && tipoUsuarioId == 1)
            {
                // Cliente só pode ver seus próprios chamados
                solicitanteUsuarioId = GetUsuarioId();
            }

            var result = await _chamadosService.ListarAsync(
                GetTenantId(), statusId, responsavelUsuarioId, solicitanteUsuarioId, page, pageSize, ct);
            return Ok(result);
        }

        [HttpGet("{id:long}")]
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
        public async Task<ActionResult<ChamadoDetailDto>> Criar(
            [FromBody] CriarChamadoRequest request,
            CancellationToken ct = default)
        {
            var result = await _chamadosService.CriarAsync(GetTenantId(), request, ct);
            return CreatedAtAction(nameof(Obter), new { id = result.ChamadoId }, result);
        }

        [HttpGet("{id:long}/interacoes")]
        public async Task<ActionResult<IReadOnlyList<InteracaoDto>>> ListarInteracoes(
            long id,
            CancellationToken ct = default)
        {
            var result = await _chamadosService.ListarInteracoesAsync(id, GetTenantId(), ct);
            return Ok(result);
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
        public async Task<IActionResult> AlterarStatus(
            long id,
            [FromBody] AlterarStatusRequest request,
            CancellationToken ct = default)
        {
            var result = await _chamadosService.AlterarStatusAsync(
                GetTenantId(), id, GetUsuarioId(), request, ct);
            return Ok(result);
        }

        [HttpGet("estatisticas")]
        public async Task<ActionResult<EstatisticasChamadosDto>> ObterEstatisticas(CancellationToken ct = default)
        {
            // Se for Cliente (TipoUsuarioId = 1), filtrar apenas seus próprios chamados
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
