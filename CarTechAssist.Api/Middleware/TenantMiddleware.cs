using System.Security.Claims;
using System.Text.Json;

namespace CarTechAssist.Api.Middleware
{



    public class TenantMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TenantMiddleware> _logger;

        public TenantMiddleware(RequestDelegate next, ILogger<TenantMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
            var isPublicEndpoint = path.Contains("/auth/login") || 
                                   path.Contains("/health") || 
                                   path.Contains("/swagger") ||
                                   path.Contains("/api/setup") ||
                                   path.Contains("/api/usuarios/registro-publico") ||
                                   path.Contains("/api/recuperacaosenha") ||
                                   path == "/" ||
                                   path.StartsWith("/swagger");

            if (isPublicEndpoint)
            {
                await _next(context);
                return;
            }

            // Adiciona headers do JWT se não existirem no request
            // Isso facilita o trabalho dos controllers
            if (!context.Request.Headers.ContainsKey("X-Tenant-Id"))
            {
                var tenantIdClaim = context.User?.FindFirst("TenantId")?.Value;
                if (!string.IsNullOrEmpty(tenantIdClaim))
                {
                    context.Request.Headers["X-Tenant-Id"] = tenantIdClaim;
                    _logger.LogDebug("TenantMiddleware: Adicionado X-Tenant-Id do JWT: {TenantId}", tenantIdClaim);
                }
            }

            if (!context.Request.Headers.ContainsKey("X-Usuario-Id"))
            {
                var usuarioIdClaim = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(usuarioIdClaim))
                {
                    context.Request.Headers["X-Usuario-Id"] = usuarioIdClaim;
                    _logger.LogDebug("TenantMiddleware: Adicionado X-Usuario-Id do JWT: {UsuarioId}", usuarioIdClaim);
                }
            }

            // Valida o TenantId SE ele existir no header
            // Se não existir, deixa os controllers tratarem (eles podem usar o JWT como fallback)
            var tenantIdHeader = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(tenantIdHeader))
            {
                if (!int.TryParse(tenantIdHeader, out var tenantId) || tenantId <= 0)
                {
                    _logger.LogWarning("TenantMiddleware: TenantId inválido no header: {TenantId}", tenantIdHeader);
                    context.Response.StatusCode = 400;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "TenantId inválido no header X-Tenant-Id. Deve ser um número inteiro positivo." }));
                    return;
                }

                // Valida se o TenantId do header corresponde ao JWT (se autenticado)
                if (context.User?.Identity?.IsAuthenticated == true)
                {
                    var jwtTenantId = context.User.FindFirst("TenantId")?.Value;
                    if (!string.IsNullOrEmpty(jwtTenantId) && int.TryParse(jwtTenantId, out var jwtTenant))
                    {
                        if (jwtTenant != tenantId)
                        {
                            _logger.LogWarning("TenantMiddleware: TenantId do header ({HeaderTenantId}) não corresponde ao JWT ({JwtTenantId})", tenantId, jwtTenant);
                            context.Response.StatusCode = 403;
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "TenantId do header não corresponde ao token JWT." }));
                            return;
                        }
                    }
                }
            }


            var usuarioIdHeader = context.Request.Headers["X-Usuario-Id"].FirstOrDefault();
            if (!string.IsNullOrEmpty(usuarioIdHeader))
            {
                if (!int.TryParse(usuarioIdHeader, out var usuarioId) || usuarioId <= 0)
                {
                    context.Response.StatusCode = 400;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "UsuarioId inválido no header X-Usuario-Id. Deve ser um número inteiro positivo." }));
                    return;
                }

                if (context.User?.Identity?.IsAuthenticated == true)
                {
                    var jwtUsuarioId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (!string.IsNullOrEmpty(jwtUsuarioId) && int.TryParse(jwtUsuarioId, out var jwtUsuario))
                    {
                        if (jwtUsuario != usuarioId)
                        {
                            _logger.LogWarning("UsuarioId do header ({HeaderUsuarioId}) não corresponde ao JWT ({JwtUsuarioId})", usuarioId, jwtUsuario);
                            context.Response.StatusCode = 403;
                            context.Response.ContentType = "application/json";
                            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "UsuarioId do header não corresponde ao token JWT." }));
                            return;
                        }
                    }
                }
            }

            await _next(context);
        }
    }

    public static class TenantMiddlewareExtensions
    {
        public static IApplicationBuilder UseTenantMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<TenantMiddleware>();
        }
    }
}

