using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data.Common;

namespace CarTechAssist.Api.Middleware
{
    public class GlobalExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        public GlobalExceptionHandlerMiddleware(
            RequestDelegate next, 
            ILogger<GlobalExceptionHandlerMiddleware> logger,
            IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro não tratado: {Message}", ex.Message);
                await HandleExceptionAsync(context, ex);
            }
        }

        private Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var statusCode = HttpStatusCode.InternalServerError;
            var title = "Ocorreu um erro interno no servidor.";
            var detail = _environment.IsDevelopment() ? exception.Message : null;
            var traceId = context.TraceIdentifier;

            switch (exception)
            {
                case SqlException sqlEx:
                    statusCode = HttpStatusCode.InternalServerError;
                    title = "Erro ao conectar com o banco de dados.";
                    detail = _environment.IsDevelopment() 
                        ? $"SQL Error: {sqlEx.Message} (Number: {sqlEx.Number})"
                        : "Verifique a connection string e se o SQL Server está rodando.";
                    break;
                case DbException dbEx:
                    statusCode = HttpStatusCode.InternalServerError;
                    title = "Erro de banco de dados.";
                    detail = _environment.IsDevelopment() ? dbEx.Message : "Erro ao acessar o banco de dados.";
                    break;
                case InvalidOperationException:
                    if (exception.Message.Contains("Connection") || exception.Message.Contains("database"))
                    {
                        statusCode = HttpStatusCode.InternalServerError;
                        title = "Erro de configuração do banco de dados.";
                        detail = exception.Message;
                    }
                    else
                    {
                        statusCode = HttpStatusCode.BadRequest;
                        title = exception.Message;
                        detail = _environment.IsDevelopment() ? exception.StackTrace : null;
                    }
                    break;
                case ArgumentException:
                    statusCode = HttpStatusCode.BadRequest;
                    title = exception.Message;
                    detail = _environment.IsDevelopment() ? exception.StackTrace : null;
                    break;
                case UnauthorizedAccessException:
                    statusCode = HttpStatusCode.Unauthorized;
                    title = exception.Message;
                    detail = _environment.IsDevelopment() ? exception.StackTrace : null;
                    break;
                case KeyNotFoundException:
                case FileNotFoundException:
                    statusCode = HttpStatusCode.NotFound;
                    title = exception.Message;
                    detail = _environment.IsDevelopment() ? exception.StackTrace : null;
                    break;
                default:
                    detail = _environment.IsDevelopment() ? exception.ToString() : null;
                    break;
            }

            var problemDetails = new ProblemDetails
            {
                Type = $"https://httpstatuses.com/{(int)statusCode}",
                Title = title,
                Status = (int)statusCode,
                Detail = detail,
                Instance = context.Request.Path
            };

            if (!string.IsNullOrEmpty(traceId))
            {
                problemDetails.Extensions["traceId"] = traceId;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = _environment.IsDevelopment()
            };

            var result = JsonSerializer.Serialize(problemDetails, options);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            return context.Response.WriteAsync(result);
        }
    }

    public static class GlobalExceptionHandlerMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<GlobalExceptionHandlerMiddleware>();
        }
    }
}

