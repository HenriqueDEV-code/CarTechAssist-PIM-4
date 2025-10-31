using System.Security.Claims;

namespace CarTechAssist.Api.Middleware
{
    public class TenantMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Se já tem TenantId no header, mantém
            if (!context.Request.Headers.ContainsKey("X-Tenant-Id"))
            {
                // Tenta extrair do JWT
                var tenantIdClaim = context.User?.FindFirst("TenantId")?.Value;
                if (tenantIdClaim != null)
                {
                    context.Request.Headers["X-Tenant-Id"] = tenantIdClaim;
                }
            }

            // Se já tem UsuarioId no header, mantém
            if (!context.Request.Headers.ContainsKey("X-Usuario-Id"))
            {
                // Tenta extrair do JWT
                var usuarioIdClaim = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (usuarioIdClaim != null)
                {
                    context.Request.Headers["X-Usuario-Id"] = usuarioIdClaim;
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

