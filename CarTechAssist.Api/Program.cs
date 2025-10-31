using System.Data;
using Microsoft.Data.SqlClient;
using System.Text;
using CarTechAssist.Api.Middleware;
using CarTechAssist.Application.Services;
using CarTechAssist.Application.Validators;
using CarTechAssist.Domain.Interfaces;
using CarTechAssist.Infrastruture.Repositories;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace CarTechAssist.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                var builder = WebApplication.CreateBuilder(args);
                var configuration = builder.Configuration;

            // Database Connection
            var connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? throw new InvalidOperationException("Connection string não configurada.");
            
            // Adiciona configurações de pooling e timeout à connection string se não existirem
            var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString)
            {
                // Configurações de performance e confiabilidade
                MaxPoolSize = 100, // Máximo de conexões no pool
                MinPoolSize = 5,   // Mínimo de conexões no pool
                ConnectTimeout = 30, // Timeout de conexão em segundos
                // Habilita retry logic (SQL Server 2019+)
                // Múltiplas tentativas de conexão
                ConnectRetryCount = 3,
                ConnectRetryInterval = 10,
                // Configurações adicionais de segurança e performance
                TrustServerCertificate = builder.Environment.IsDevelopment(), // Permite certificado self-signed em dev
                Encrypt = true // Sempre criptografar conexão
            };
            
            builder.Services.AddScoped<IDbConnection>(sp =>
                new SqlConnection(connectionStringBuilder.ConnectionString));

            // Repositories
            builder.Services.AddScoped<IChamadosRepository, ChamadosRepository>();
            builder.Services.AddScoped<IUsuariosRepository, UsuariosRepository>();
            builder.Services.AddScoped<ICategoriasRepository, CategoriasRepository>();
            builder.Services.AddScoped<IAnexosReposity, AnexosRepository>();
            builder.Services.AddScoped<Domain.Interfaces.IRecuperacaoSenhaRepository, RecuperacaoSenhaRepository>();

            // Services
            builder.Services.AddScoped<ChamadosService>();
            builder.Services.AddScoped<UsuariosService>();
            builder.Services.AddScoped<AuthService>();
            builder.Services.AddScoped<CategoriasService>();
            builder.Services.AddScoped<EmailService>();
            builder.Services.AddScoped<RecuperacaoSenhaService>();

            // JWT Authentication
            var jwtSecretKey = configuration["Jwt:SecretKey"] 
                ?? Environment.GetEnvironmentVariable("JWT__SecretKey")
                ?? throw new InvalidOperationException("JWT SecretKey não configurada. Verifique appsettings.json ou appsettings.Development.json");
            
            if (string.IsNullOrWhiteSpace(jwtSecretKey) || jwtSecretKey.Length < 32)
            {
                throw new InvalidOperationException(
                    $"JWT SecretKey está vazia ou muito curta (mínimo 32 caracteres). Valor atual: {(string.IsNullOrEmpty(jwtSecretKey) ? "(vazio)" : $"{jwtSecretKey.Length} caracteres")}");
            }
            
            var jwtIssuer = configuration["Jwt:Issuer"] 
                ?? Environment.GetEnvironmentVariable("JWT__Issuer")
                ?? "CarTechAssist";
            var jwtAudience = configuration["Jwt:Audience"] 
                ?? Environment.GetEnvironmentVariable("JWT__Audience")
                ?? "CarTechAssist";

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

            // CORS
            builder.Services.AddCors(options =>
            {
                var allowedOrigins = configuration["Cors:AllowedOrigins"]?.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    ?? (builder.Environment.IsDevelopment() 
                        ? new[] { 
                            "http://localhost:3000", 
                            "http://localhost:5173",
                            "http://localhost:5095",
                            "https://localhost:7045"
                        }
                        : Array.Empty<string>());

                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins(allowedOrigins)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                });
            });

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // FluentValidation - registra validators automaticamente instalados nos assemblies referenciados
            builder.Services.AddFluentValidationAutoValidation();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // Global Exception Handler (deve ser o primeiro)
            app.UseGlobalExceptionHandler();

            app.UseCors();

            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();

            // Middleware para extrair TenantId e UsuarioId do JWT
            app.UseTenantMiddleware();

            app.MapControllers();

            app.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ ERRO FATAL ao iniciar a aplicação:");
                Console.WriteLine($"   Tipo: {ex.GetType().Name}");
                Console.WriteLine($"   Mensagem: {ex.Message}");
                Console.WriteLine("");
                Console.WriteLine("📋 DETALHES:");
                Console.WriteLine(ex.ToString());
                Console.WriteLine("");
                Console.WriteLine("💡 VERIFICAÇÕES:");
                
                if (ex.Message.Contains("JWT") || ex.Message.Contains("SecretKey"))
                {
                    Console.WriteLine("   → Verifique se a chave JWT está configurada em appsettings.json ou appsettings.Development.json");
                    Console.WriteLine("   → A chave deve ter pelo menos 32 caracteres");
                }
                
                if (ex.Message.Contains("Connection") || ex.Message.Contains("database"))
                {
                    Console.WriteLine("   → Verifique se a connection string está configurada");
                    Console.WriteLine("   → Verifique se o SQL Server está rodando");
                }
                
                Console.WriteLine("");
                Console.WriteLine("Pressione qualquer tecla para sair...");
                Console.ReadKey();
                throw; // Re-lança para o sistema operacional registrar o erro
            }
        }
    }
}
