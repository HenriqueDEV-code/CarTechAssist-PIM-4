using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace CarTechAssist.Api.Attributes
{
    /// <summary>
    /// Atributo de autorização baseado em roles do sistema.
    /// Roles: 1=Cliente, 2=Técnico, 3=Administrador, 4=Bot
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class AuthorizeRolesAttribute : AuthorizeAttribute, IAuthorizationFilter
    {
        private readonly byte[] _allowedRoles;

        /// <summary>
        /// Define quais roles podem acessar o endpoint
        /// </summary>
        /// <param name="allowedRoles">Array de bytes representando os IDs das roles permitidas (1=Cliente, 2=Técnico, 3=Admin)</param>
        public AuthorizeRolesAttribute(params byte[] allowedRoles)
        {
            _allowedRoles = allowedRoles;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Verificar se o usuário está autenticado
            if (context.HttpContext.User?.Identity?.IsAuthenticated != true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Obter role do usuário do JWT
            var logger = context.HttpContext.RequestServices.GetService<ILogger<AuthorizeRolesAttribute>>();
            var userId = context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            
            // Listar todas as claims para debug
            var allClaims = context.HttpContext.User.Claims.Select(c => $"{c.Type}={c.Value}").ToList();
            logger?.LogDebug("Todas as claims do usuário {UserId}: {Claims}", userId, string.Join(", ", allClaims));
            
            var roleClaim = context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Role);
            
            if (roleClaim == null)
            {
                logger?.LogWarning("Role claim não encontrado no JWT para usuário: {UserId}. Claims disponíveis: {Claims}", 
                    userId, string.Join(", ", allClaims));
                context.Result = new ObjectResult(new { message = "Role não encontrada no token de acesso. Por favor, faça logout e login novamente." })
                {
                    StatusCode = 403
                };
                return;
            }

            // Tentar parsear como byte - pode vir como string "1", "2", etc.
            // IMPORTANTE: Se vier como nome do enum ("Cliente", "Tecnico", etc), converter para número
            byte userRole;
            var roleValue = roleClaim.Value?.Trim() ?? string.Empty;
            
            logger?.LogDebug("Tentando parsear role claim. Valor: '{RoleValue}' (Length: {Length})", roleValue, roleValue.Length);
            
            // Mapear nomes do enum para números (caso venha como string do enum)
            var roleMap = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
            {
                { "Cliente", 1 },
                { "Tecnico", 2 },
                { "Técnico", 2 },
                { "Administrador", 3 },
                { "Bot", 4 },
                { "1", 1 },
                { "2", 2 },
                { "3", 3 },
                { "4", 4 }
            };
            
            if (roleMap.TryGetValue(roleValue, out userRole))
            {
                logger?.LogInformation("Role mapeada de '{RoleValue}' para {UserRole}", roleValue, userRole);
            }
            else if (byte.TryParse(roleValue, out userRole))
            {
                logger?.LogInformation("Role parseada diretamente como byte: {UserRole}", userRole);
            }
            else
            {
                logger?.LogError("Role claim não pôde ser convertido. Valor: '{RoleValue}'. Claims completas: {Claims}", 
                    roleValue, string.Join(", ", allClaims));
                context.Result = new ObjectResult(new { 
                    message = $"Role inválida no token de acesso. Valor encontrado: '{roleValue}'. Por favor, faça logout e login novamente para renovar o token com o formato correto (número 1-4)." 
                })
                {
                    StatusCode = 403
                };
                return;
            }
            
            logger?.LogInformation("Role do usuário no JWT: {UserRole}, Roles permitidas: {AllowedRoles}", 
                userRole, string.Join(", ", _allowedRoles));

            // Verificar se a role do usuário está nas roles permitidas
            if (!_allowedRoles.Contains(userRole))
            {
                logger?.LogWarning("Acesso negado. Role do usuário: {UserRole}, Roles permitidas: {AllowedRoles}", 
                    userRole, string.Join(", ", _allowedRoles));
                context.Result = new ObjectResult(new { message = "Você não tem permissão para usar o ChatBot. Esta funcionalidade é apenas para clientes." })
                {
                    StatusCode = 403
                };
                return;
            }
        }
    }
}

