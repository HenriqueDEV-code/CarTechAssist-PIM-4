using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CarTechAssist.Contracts.Auth;
using CarTechAssist.Domain.Enums;
using CarTechAssist.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace CarTechAssist.Application.Services
{
    public class AuthService
    {
        private readonly IUsuariosRepository _usuariosRepository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IUsuariosRepository usuariosRepository, IConfiguration configuration, ILogger<AuthService> logger)
        {
            _usuariosRepository = usuariosRepository;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<LoginResponse?> AutenticarAsync(LoginRequest request, CancellationToken ct)
        {
            _logger.LogInformation("Tentativa de login. Tenant: {TenantId}, Login: {Login}", request.TenantId, request.Login);
            
            // Buscar usuário por login
            var usuario = await _usuariosRepository.ObterPorLoginAsync(request.TenantId, request.Login, ct);
            if (usuario == null || !usuario.Ativo || usuario.Excluido)
            {
                _logger.LogWarning("Falha no login. Usuário não encontrado, inativo ou excluído. Tenant: {TenantId}, Login: {Login}", 
                    request.TenantId, request.Login);
                return null;
            }

            // Verificar senha
            if (usuario.HashSenha == null || usuario.SaltSenha == null)
            {
                _logger.LogWarning("Usuário sem senha configurada. UsuarioId: {UsuarioId}, Login: {Login}. Use o endpoint /api/Setup/definir-senha-admin para definir a senha.", 
                    usuario.UsuarioId, usuario.Login);
                throw new InvalidOperationException($"Usuário '{usuario.Login}' não possui senha configurada. Use o endpoint POST /api/Setup/definir-senha-admin para definir uma senha.");
            }

            if (!VerificarSenha(request.Senha, usuario.HashSenha, usuario.SaltSenha))
            {
                _logger.LogWarning("Senha incorreta. UsuarioId: {UsuarioId}, Login: {Login}", usuario.UsuarioId, usuario.Login);
                return null;
            }

            _logger.LogInformation("Login bem-sucedido. UsuarioId: {UsuarioId}, Login: {Login}", usuario.UsuarioId, usuario.Login);

            // Gerar tokens
            var token = GerarJwtToken(usuario);
            var refreshToken = GerarRefreshToken();

            return new LoginResponse(
                token,
                refreshToken,
                usuario.UsuarioId,
                usuario.NomeCompleto,
                usuario.TenantId,
                (byte)usuario.TipoUsuarioId
            );
        }

        private string GerarJwtToken(Domain.Entities.Usuario usuario)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey não configurada")));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Garantir que o TipoUsuarioId seja convertido para byte (número) e não para string do enum
            var tipoUsuarioIdNumero = ((byte)usuario.TipoUsuarioId).ToString();
            
            _logger.LogDebug("Gerando JWT token. TipoUsuarioId (enum): {TipoUsuarioId}, TipoUsuarioId (número): {TipoUsuarioIdNumero}", 
                usuario.TipoUsuarioId, tipoUsuarioIdNumero);
            
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.UsuarioId.ToString()),
                new Claim(ClaimTypes.Name, usuario.Login),
                new Claim("TenantId", usuario.TenantId.ToString()),
                new Claim("NomeCompleto", usuario.NomeCompleto),
                new Claim(ClaimTypes.Role, tipoUsuarioIdNumero) // Usar número, não string do enum
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static string GerarRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        private static bool VerificarSenha(string senha, byte[] hash, byte[] salt)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA512(salt);
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(senha));
            
            if (computedHash.Length != hash.Length)
                return false;

            for (int i = 0; i < computedHash.Length; i++)
            {
                if (computedHash[i] != hash[i])
                    return false;
            }

            return true;
        }

        public async Task<UsuarioLogadoDto?> ObterUsuarioLogadoAsync(int tenantId, int usuarioId, CancellationToken ct)
        {
            _logger.LogInformation("Obtendo informações do usuário logado. UsuarioId: {UsuarioId}, TenantId: {TenantId}", usuarioId, tenantId);

            var usuario = await _usuariosRepository.ObterPorIdAsync(usuarioId, ct);
            if (usuario == null || usuario.TenantId != tenantId || !usuario.Ativo || usuario.Excluido)
            {
                _logger.LogWarning("Usuário não encontrado ou inválido. UsuarioId: {UsuarioId}, TenantId: {TenantId}", usuarioId, tenantId);
                return null;
            }

            return new UsuarioLogadoDto(
                usuario.UsuarioId,
                usuario.TenantId,
                usuario.Login,
                usuario.NomeCompleto,
                usuario.Email,
                usuario.Telefone,
                (byte)usuario.TipoUsuarioId,
                usuario.TipoUsuarioId.ToString()
            );
        }

        public Task<LoginResponse?> RenovarTokenAsync(string refreshToken, CancellationToken ct)
        {
            _logger.LogInformation("Tentativa de renovação de token com refresh token");
            
            // TODO: Implementar validação do refresh token no banco de dados
            // Por enquanto, apenas retorna null para indicar que precisa fazer login novamente
            // Em produção, você deve:
            // 1. Armazenar refresh tokens no banco (ex: tabela RefreshToken)
            // 2. Validar se o token existe e não está expirado
            // 3. Buscar o usuário associado ao token
            // 4. Gerar novo JWT e novo refresh token
            // 5. Invalidar o refresh token antigo
            
            _logger.LogWarning("Refresh token não implementado. Retornando null.");
            return Task.FromResult<LoginResponse?>(null);
        }
    }
}

