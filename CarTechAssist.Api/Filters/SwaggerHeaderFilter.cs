using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;

namespace CarTechAssist.Api.Filters
{
    /// <summary>
    /// Filtro para adicionar headers customizados no Swagger UI
    /// </summary>
    public class SwaggerHeaderFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation.Parameters == null)
                operation.Parameters = new List<OpenApiParameter>();

            // Adicionar header X-Tenant-Id para endpoints que precisam
            var requiresAuth = context.MethodInfo.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false).Length > 0
                            || context.MethodInfo.DeclaringType?.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false).Length > 0;

            if (requiresAuth)
            {
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "X-Tenant-Id",
                    In = ParameterLocation.Header,
                    Description = "ID do Tenant (obrigatório para endpoints autenticados)",
                    Required = false, // Não obrigatório no Swagger, mas será validado no controller
                    Schema = new OpenApiSchema
                    {
                        Type = "integer",
                        Format = "int32"
                    }
                });

                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = "X-Usuario-Id",
                    In = ParameterLocation.Header,
                    Description = "ID do Usuário (opcional, pode ser extraído do JWT)",
                    Required = false,
                    Schema = new OpenApiSchema
                    {
                        Type = "integer",
                        Format = "int32"
                    }
                });
            }
        }
    }
}

