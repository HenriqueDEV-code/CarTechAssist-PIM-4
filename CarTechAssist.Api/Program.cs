using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.SignalR;
using CarTechAssist.Application.Services;
using CarTechAssist.Domain.Interfaces;
using CarTechAssist.Infrastruture.Repositories;
using CarTechAssist.Api.Hubs;
using CarTechAssist.Api.Middleware;
using CarTechAssist.Api.Services;
using CarTechAssist.Api.Filters;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using FluentValidation;
using FluentValidation.AspNetCore;
using CarTechAssist.Application.Validators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;

namespace CarTechAssist.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var configuration = builder.Configuration;

            // Database Connection
            builder.Services.AddScoped<IDbConnection>(sp =>
                new SqlConnection(configuration.GetConnectionString("DefaultConnection")));

            // Repositories
            builder.Services.AddScoped<IChamadosRepository, ChamadosRepository>();
            builder.Services.AddScoped<IUsuariosRepository, UsuariosRepository>();
            builder.Services.AddScoped<IRecuperacaoSenhaRepository, RecuperacaoSenhaRepository>();
            builder.Services.AddScoped<ICategoriasRepository, CategoriasRepository>();
            builder.Services.AddScoped<IAnexosReposity, AnexosRepository>();
            builder.Services.AddScoped<IFeedbackRepository, FeedbackRepository>();
            builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

            // HttpClient para OpenRouter
            builder.Services.AddHttpClient();

            // Services
            builder.Services.AddScoped<AuthService>();
            builder.Services.AddScoped<EmailService>();
            builder.Services.AddScoped<RecuperacaoSenhaService>();
            builder.Services.AddScoped<ChamadosService>();
            builder.Services.AddScoped<UsuariosService>();
            builder.Services.AddScoped<DialogflowService>();
            builder.Services.AddScoped<OpenRouterService>();
            builder.Services.AddScoped<CategoriasService>();
            
           
            builder.Services.AddSingleton<CarTechAssist.Application.Services.InputSanitizer>();
            
            
            builder.Services.AddHostedService<RefreshTokenCleanupService>();

            // JWT Authentication
            var jwtSecretKey = configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey não configurada");
            var jwtIssuer = configuration["Jwt:Issuer"] ?? "CarTechAssist";
            var jwtAudience = configuration["Jwt:Audience"] ?? "CarTechAssist";

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
                    ClockSkew = TimeSpan.Zero
                };
            });

            builder.Services.AddAuthorization();

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo 
                { 
                    Title = "CarTechAssist API", 
                    Version = "v1",
                    Description = "API para gerenciamento de chamados técnicos multi-tenant"
                });

                // CORREÇÃO: Configurar autenticação JWT no Swagger
                c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Description = @"JWT Authorization header usando o esquema Bearer. 
                                    
**INSTRUÇÕES:**
1. Primeiro, faça login no endpoint POST /api/Auth/login
2. Copie o token da resposta (campo 'token')
3. Clique no botão 'Authorize' acima
4. Cole o token no campo 'Value' (sem a palavra 'Bearer')
5. Clique em 'Authorize' e depois em 'Close'",
                    Name = "Authorization",
                    In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                    Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT"
                });

                c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
                {
                    {
                        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                        {
                            Reference = new Microsoft.OpenApi.Models.OpenApiReference
                            {
                                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });

                // CORREÇÃO: Adicionar suporte para headers customizados no Swagger
                c.OperationFilter<SwaggerHeaderFilter>();
            });
            
            // CORREÇÃO CRÍTICA: Registrar FluentValidation para validação automática (API moderna, não obsoleta)
            builder.Services.AddValidatorsFromAssemblyContaining<CriarChamadoRequestValidator>();
            builder.Services.AddFluentValidationAutoValidation();
            builder.Services.AddFluentValidationClientsideAdapters();

            // CORREÇÃO: Compressão de respostas para melhor performance
            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
            });
            builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Optimal;
            });
            builder.Services.Configure<GzipCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Optimal;
            });

            // CORREÇÃO: Limite de tamanho de arquivo no upload
            builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 10485760; // 10MB
            });

            // SignalR para chat em tempo real
            builder.Services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = builder.Environment.IsDevelopment();
            });

            // Memory Cache para Rate Limiting
            builder.Services.AddMemoryCache();

            // Rate Limiting
            builder.Services.Configure<IpRateLimitOptions>(options =>
            {
                options.EnableEndpointRateLimiting = true;
                options.StackBlockedRequests = false;
                options.HttpStatusCode = 429;
                options.RealIpHeader = "X-Real-IP";
                options.GeneralRules = new List<RateLimitRule>
                {
                    new RateLimitRule
                    {
                        Endpoint = "*",
                        Period = "1m",
                        Limit = 100
                    },
                    new RateLimitRule
                    {
                        Endpoint = "POST:/api/Auth/login",
                        Period = "1m",
                        Limit = 5
                    },
                    // CORREÇÃO CRÍTICA: Rate limiting no endpoint refresh token
                    new RateLimitRule
                    {
                        Endpoint = "POST:/api/Auth/refresh",
                        Period = "1m",
                        Limit = 10
                    }
                };
            });

            builder.Services.AddInMemoryRateLimiting();
            builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

            // CORS - Permitir apenas origens específicas
            var allowedOrigins = configuration["AllowedOrigins"]?.Split(',') 
                ?? new[] { "http://localhost:5095", "https://localhost:7045" };

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigins", policy =>
                {
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                });
            });

            // Health Checks
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddHealthChecks()
                .AddSqlServer(connectionString ?? throw new InvalidOperationException("Connection string não configurada"), 
                    name: "database", 
                    timeout: TimeSpan.FromSeconds(5));

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            // CORREÇÃO: Compressão de respostas
            app.UseResponseCompression();

            // Middleware de tratamento global de exceções (DEVE vir primeiro)
            app.UseGlobalExceptionHandler();

            // Rate Limiting
            app.UseIpRateLimiting();

            // Tenant Middleware
            app.UseTenantMiddleware();

            // CORS deve vir antes de UseAuthentication/UseAuthorization
            app.UseCors("AllowSpecificOrigins");

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            // Mapear SignalR Hub
            app.MapHub<Hubs.ChamadoHub>("/hubs/chamado");

            // Health Check endpoint
            app.MapHealthChecks("/health");

            app.Run();
        }
    }
}
