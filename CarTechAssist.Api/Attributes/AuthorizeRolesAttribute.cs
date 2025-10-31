using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

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
            var roleClaim = context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Role);
            if (roleClaim == null || !byte.TryParse(roleClaim.Value, out var userRole))
            {
                context.Result = new ForbidResult();
                return;
            }

            // Verificar se a role do usuário está nas roles permitidas
            if (!_allowedRoles.Contains(userRole))
            {
                context.Result = new ForbidResult();
                return;
            }
        }
    }
}

