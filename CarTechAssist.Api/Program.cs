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

            // Services
            builder.Services.AddScoped<AuthService>();
            builder.Services.AddScoped<EmailService>();
            builder.Services.AddScoped<RecuperacaoSenhaService>();
            builder.Services.AddScoped<ChamadosService>();
            builder.Services.AddScoped<UsuariosService>();
            builder.Services.AddScoped<DialogflowService>();
            builder.Services.AddScoped<ChatBotService>();
            builder.Services.AddScoped<CategoriasService>();

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
            builder.Services.AddSwaggerGen();

            // SignalR para chat em tempo real
            builder.Services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = builder.Environment.IsDevelopment();
            });

            // CORS - Permitir requisições do frontend
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            // CORS deve vir antes de UseAuthentication/UseAuthorization
            app.UseCors("AllowAll");

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            // Mapear SignalR Hub
            app.MapHub<Hubs.ChamadoHub>("/hubs/chamado");

            app.Run();
        }
    }
}
