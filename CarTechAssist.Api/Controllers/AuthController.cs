using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CarTechAssist.Application.Services;
using CarTechAssist.Contracts.Auth;

namespace CarTechAssist.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login(
            [FromBody] LoginRequest request,
            CancellationToken ct = default)
        {
            try
            {
                var result = await _authService.AutenticarAsync(request, ct);
                if (result == null)
                    return Unauthorized(new { message = "Login ou senha inválidos.", hint = "Verifique se o usuário existe e se a senha está correta." });

                return Ok(result);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("não possui senha"))
            {
                return Unauthorized(new 
                { 
                    message = ex.Message,
                    solution = "Use o endpoint POST /api/Setup/definir-senha-admin no Swagger para definir a senha.",
                    endpoint = "/api/Setup/definir-senha-admin",
                    body = new { tenantId = request.TenantId, login = request.Login, novaSenha = "Admin@123" }
                });
            }
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<LoginResponse>> Refresh(
            [FromBody] RefreshTokenRequest request,
            CancellationToken ct = default)
        {
            var result = await _authService.RenovarTokenAsync(request.RefreshToken, ct);
            if (result == null)
                return Unauthorized("Refresh token inválido ou expirado.");

            return Ok(result);
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<UsuarioLogadoDto>> Me(CancellationToken ct = default)
        {
            var tenantIdHeader = Request.Headers["X-Tenant-Id"].FirstOrDefault();
            var usuarioIdHeader = Request.Headers["X-Usuario-Id"].FirstOrDefault();

            if (string.IsNullOrEmpty(tenantIdHeader) || !int.TryParse(tenantIdHeader, out var tenantId))
                return Unauthorized("TenantId não encontrado ou inválido no header X-Tenant-Id.");

            if (string.IsNullOrEmpty(usuarioIdHeader) || !int.TryParse(usuarioIdHeader, out var usuarioId))
                return Unauthorized("UsuarioId não encontrado ou inválido no header X-Usuario-Id.");

            var result = await _authService.ObterUsuarioLogadoAsync(tenantId, usuarioId, ct);
            if (result == null)
                return NotFound("Usuário não encontrado.");

            return Ok(result);
        }
    }
}

