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
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<ChamadosController>>();
            try
            {
                var result = await _chamadosService.ListarInteracoesAsync(id, GetTenantId(), ct);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogError(ex, "Erro de autorização ao listar interações do chamado {ChamadoId}", id);
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao listar interações do chamado {ChamadoId}", id);
                return StatusCode(500, new { message = "Erro ao listar interações.", error = ex.Message });
            }
        }

        [HttpPost("{id:long}/interacoes")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> AdicionarInteracao(
            long id,
            [FromBody] AdicionarInteracaoRequest? request,
            CancellationToken ct = default)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<ChamadosController>>();
            
            try
            {
                // Validação do request
                if (request == null || string.IsNullOrWhiteSpace(request.Mensagem))
                {
                    logger.LogWarning("Request nulo ou mensagem vazia ao adicionar interação ao chamado {ChamadoId}", id);
                    return BadRequest(new { message = "Request inválido. O campo 'mensagem' é obrigatório." });
                }

                // Validação de autenticação
                if (User?.Identity?.IsAuthenticated != true)
                {
                    logger.LogWarning("Tentativa de adicionar interação sem autenticação. ChamadoId: {ChamadoId}", id);
                    return Unauthorized(new { message = "Não autenticado. Faça login primeiro." });
                }

                var tenantId = GetTenantId();
                var usuarioId = GetUsuarioId();
                
                logger.LogInformation("Adicionando interação ao chamado {ChamadoId}. TenantId: {TenantId}, UsuarioId: {UsuarioId}", 
                    id, tenantId, usuarioId);

                var result = await _chamadosService.AdicionarInteracaoAsync(
                    tenantId, id, usuarioId, request, ct);
                    
                logger.LogInformation("Interação adicionada com sucesso ao chamado {ChamadoId}. InteracaoId: {InteracaoId}", 
                    id, result.InteracaoId);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning(ex, "Erro de validação ao adicionar interação ao chamado {ChamadoId}: {Message}", id, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogError(ex, "Erro de autorização ao adicionar interação ao chamado {ChamadoId}: {Message}", id, ex.Message);
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro inesperado ao adicionar interação ao chamado {ChamadoId}. Tipo: {Type}, Message: {Message}, StackTrace: {StackTrace}, InnerException: {InnerException}", 
                    id, ex.GetType().Name, ex.Message, ex.StackTrace, ex.InnerException?.Message);
                return StatusCode(500, new { message = "Erro ao adicionar interação.", error = ex.Message, innerError = ex.InnerException?.Message });
            }
        }

        [HttpPost("{id:long}/anexos")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> UploadAnexo(
            long id,
            IFormFile arquivo,
            CancellationToken ct = default)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<ChamadosController>>();
            
            if (arquivo == null || arquivo.Length == 0)
                return BadRequest(new { message = "Arquivo não fornecido." });

            try
            {
                using var stream = new MemoryStream();
                await arquivo.CopyToAsync(stream, ct);
                var bytes = stream.ToArray();

                await _chamadosService.AdicionarAnexoAsync(
                    GetTenantId(), id, GetUsuarioId(), arquivo.FileName, arquivo.ContentType, bytes, ct);

                logger.LogInformation("✅ Upload anexo - Sucesso. ChamadoId: {ChamadoId}, Arquivo: {FileName}", id, arquivo.FileName);
                return Ok(new { message = "Arquivo enviado com sucesso." });
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "Erro de validação ao fazer upload de anexo no chamado {ChamadoId}", id);
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogError(ex, "Erro de autorização ao fazer upload de anexo no chamado {ChamadoId}", id);
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao fazer upload de anexo no chamado {ChamadoId}", id);
                return StatusCode(500, new { message = "Erro ao fazer upload do arquivo.", error = ex.Message });
            }
        }

        [HttpGet("{id:long}/anexos")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<ActionResult<IReadOnlyList<AnexoDto>>> ListarAnexos(
            long id,
            CancellationToken ct = default)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<ChamadosController>>();
            try
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
            catch (UnauthorizedAccessException ex)
            {
                logger.LogError(ex, "Erro de autorização ao listar anexos do chamado {ChamadoId}", id);
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao listar anexos do chamado {ChamadoId}", id);
                return StatusCode(500, new { message = "Erro ao listar anexos.", error = ex.Message });
            }
        }

        [HttpGet("{id:long}/anexos/{anexoId:long}")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> DownloadAnexo(
            long id,
            long anexoId,
            CancellationToken ct = default)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<ChamadosController>>();
            try
            {
                var anexo = await _chamadosService.ObterAnexoAsync(anexoId, GetTenantId(), ct);
                if (anexo == null || anexo.ChamadoId != id)
                    return NotFound(new { message = "Anexo não encontrado." });

                if (anexo.Conteudo == null || anexo.Conteudo.Length == 0)
                    return NotFound(new { message = "Conteúdo do anexo não disponível." });

                return File(anexo.Conteudo, anexo.ContentType ?? "application/octet-stream", anexo.NomeArquivo);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogError(ex, "Erro de autorização ao baixar anexo {AnexoId} do chamado {ChamadoId}", anexoId, id);
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao baixar anexo {AnexoId} do chamado {ChamadoId}", anexoId, id);
                return StatusCode(500, new { message = "Erro ao baixar anexo.", error = ex.Message });
            }
        }

        [HttpPatch("{id:long}/status")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> AlterarStatus(
            long id,
            [FromBody] AlterarStatusRequest? request,
            CancellationToken ct = default)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<ChamadosController>>();
            
            try
            {
                // Validação do request
                if (request == null)
                {
                    logger.LogWarning("Request nulo ao alterar status do chamado {ChamadoId}", id);
                    return BadRequest(new { message = "Request inválido. O corpo da requisição não pode estar vazio. Envie um JSON com o campo 'novoStatus' (ou 'NovoStatus')." });
                }

                // Validação do novoStatus
                if (request.NovoStatus < 1 || request.NovoStatus > 6)
                {
                    logger.LogWarning("Status inválido ao alterar status do chamado {ChamadoId}. NovoStatus: {NovoStatus}", id, request.NovoStatus);
                    return BadRequest(new { message = $"Status inválido: {request.NovoStatus}. O status deve estar entre 1 e 6." });
                }

                // Validação de autenticação
                if (User?.Identity?.IsAuthenticated != true)
                {
                    logger.LogWarning("Tentativa de alterar status sem autenticação. ChamadoId: {ChamadoId}", id);
                    return Unauthorized(new { message = "Não autenticado. Faça login primeiro." });
                }

                // Validação de permissão
                var tipoUsuarioIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                if (!byte.TryParse(tipoUsuarioIdStr, out var tipoUsuarioId) || (tipoUsuarioId != 2 && tipoUsuarioId != 3))
                {
                    logger.LogWarning("Tentativa de alterar status sem permissão. ChamadoId: {ChamadoId}, TipoUsuarioId: {TipoUsuarioId}", id, tipoUsuarioIdStr);
                    return StatusCode(403, new { message = "Apenas técnicos e administradores podem alterar o status do chamado." });
                }

                // Obter TenantId e UsuarioId
                int tenantId;
                int usuarioId;
                try
                {
                    tenantId = GetTenantId();
                    usuarioId = GetUsuarioId();
                }
                catch (UnauthorizedAccessException ex)
                {
                    logger.LogWarning(ex, "Erro ao obter TenantId/UsuarioId ao alterar status do chamado {ChamadoId}: {Message}", id, ex.Message);
                    return Unauthorized(new { message = ex.Message });
                }
                
                logger.LogInformation("Alterando status do chamado {ChamadoId}. TenantId: {TenantId}, UsuarioId: {UsuarioId}, NovoStatus: {NovoStatus}", 
                    id, tenantId, usuarioId, request.NovoStatus);

                // Chamar o serviço
                var result = await _chamadosService.AlterarStatusAsync(
                    tenantId, id, usuarioId, request, ct);
                
                if (result == null)
                {
                    logger.LogError("Serviço retornou null ao alterar status do chamado {ChamadoId}", id);
                    return StatusCode(500, new { message = "Erro ao alterar status. O serviço retornou null." });
                }
                    
                logger.LogInformation("Status do chamado {ChamadoId} alterado com sucesso para {NovoStatus}. Novo StatusId: {StatusId}", 
                    id, request.NovoStatus, result.StatusId);
                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(ex, "Erro de autorização ao alterar status do chamado {ChamadoId}: {Message}", id, ex.Message);
                return StatusCode(403, new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "Erro de validação ao alterar status do chamado {ChamadoId}: {Message}", id, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError(ex, "Erro de operação ao alterar status do chamado {ChamadoId}: {Message}", id, ex.Message);
                return StatusCode(500, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro inesperado ao alterar status do chamado {ChamadoId}. Tipo: {Type}, Message: {Message}, StackTrace: {StackTrace}, InnerException: {InnerException}", 
                    id, ex.GetType().Name, ex.Message, ex.StackTrace, ex.InnerException?.Message);
                return StatusCode(500, new { message = "Erro ao alterar status do chamado.", error = ex.Message, innerError = ex.InnerException?.Message });
            }
        }

        [HttpGet("estatisticas")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<ActionResult<EstatisticasChamadosDto>> ObterEstatisticas(CancellationToken ct = default)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<ChamadosController>>();
            
            try
            {
                var tipoUsuarioIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                int? solicitanteUsuarioId = null;
                
                if (byte.TryParse(tipoUsuarioIdStr, out var tipoUsuarioId) && tipoUsuarioId == 1)
                {
                    solicitanteUsuarioId = GetUsuarioId();
                }

                var tenantId = GetTenantId();
                logger.LogInformation("🔍 OBTER ESTATISTICAS - TenantId: {TenantId}, SolicitanteUsuarioId: {SolicitanteUsuarioId}", 
                    tenantId, solicitanteUsuarioId);

                var estatisticas = await _chamadosService.ObterEstatisticasAsync(
                    tenantId, solicitanteUsuarioId, ct);
                
                logger.LogInformation("✅ OBTER ESTATISTICAS - Sucesso. Total: {Total}, Abertos: {Abertos}", 
                    estatisticas?.Total ?? 0, estatisticas?.Abertos ?? 0);
                
                return Ok(estatisticas);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogError(ex, "❌ OBTER ESTATISTICAS - Erro de autorização: {Message}", ex.Message);
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ OBTER ESTATISTICAS - Erro inesperado: {Message}", ex.Message);
                return StatusCode(500, new { message = "Erro ao obter estatísticas. Tente novamente.", error = ex.Message });
            }
        }

        [HttpPost("{id:long}/feedback")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> EnviarFeedback(
            long id, 
            [FromBody] EnviarFeedbackRequest request,
            CancellationToken ct = default)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<ChamadosController>>();
            try
            {
                await _chamadosService.RegistrarFeedbackAsync(
                    id, 
                    GetTenantId(), 
                    GetUsuarioId(), 
                    request.Score, 
                    request.Comentario, 
                    ct);
                
                logger.LogInformation("✅ Feedback registrado - Sucesso. ChamadoId: {ChamadoId}, Score: {Score}", id, request.Score);
                return Ok(new { message = "Feedback registrado com sucesso." });
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "Erro de validação ao registrar feedback no chamado {ChamadoId}", id);
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogError(ex, "Erro de autorização ao registrar feedback no chamado {ChamadoId}", id);
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao registrar feedback no chamado {ChamadoId}", id);
                return StatusCode(500, new { message = "Erro ao registrar feedback.", error = ex.Message });
            }
        }
    }
}
