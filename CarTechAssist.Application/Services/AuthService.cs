using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CarTechAssist.Contracts.Auth;
using CarTechAssist.Domain.Entities;
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
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IUsuariosRepository usuariosRepository, 
            IRefreshTokenRepository refreshTokenRepository,
            IConfiguration configuration, 
            ILogger<AuthService> logger)
        {
            _usuariosRepository = usuariosRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<LoginResponse?> AutenticarAsync(LoginRequest request, CancellationToken ct)
        {
            _logger.LogInformation("Tentativa de login. Tenant: {TenantId}, Login: {Login}", request.TenantId, request.Login);

            var usuario = await _usuariosRepository.ObterPorLoginAsync(request.TenantId, request.Login, ct);
            if (usuario == null || !usuario.Ativo || usuario.Excluido)
            {
                _logger.LogWarning("Falha no login. Usuário não encontrado, inativo ou excluído. Tenant: {TenantId}, Login: {Login}", 
                    request.TenantId, request.Login);
                return null;
            }

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

            var token = GerarJwtToken(usuario);
            var refreshTokenString = GerarRefreshToken();

            var refreshTokenExpirationDays = int.Parse(_configuration["Jwt:RefreshTokenExpirationDays"] ?? "7");
            var refreshToken = new RefreshToken
            {
                UsuarioId = usuario.UsuarioId,
                Token = refreshTokenString,
                ExpiraEm = DateTime.UtcNow.AddDays(refreshTokenExpirationDays),
                Revogado = false,
                DataCriacao = DateTime.UtcNow
            };
            
            await _refreshTokenRepository.CriarAsync(refreshToken, ct);

            return new LoginResponse(
                token,
                refreshTokenString,
                usuario.UsuarioId,
                usuario.NomeCompleto,
                usuario.TenantId,
                (byte)usuario.TipoUsuarioId
            );
        }

        private string GerarJwtToken(Domain.Entities.Usuario usuario)
        {
            var jwtSecretKey = _configuration["Jwt:SecretKey"];
            if (string.IsNullOrWhiteSpace(jwtSecretKey))
            {
                throw new InvalidOperationException(
                    "JWT SecretKey não configurada ou está vazia. " +
                    "Configure uma chave secreta válida no appsettings.json na seção 'Jwt:SecretKey'. " +
                    "A chave deve ter pelo menos 32 caracteres para segurança adequada.");
            }

            // Validar comprimento mínimo da chave (32 bytes = 256 bits para HMAC-SHA256)
            if (Encoding.UTF8.GetByteCount(jwtSecretKey) < 32)
            {
                throw new InvalidOperationException(
                    $"JWT SecretKey deve ter pelo menos 32 caracteres (bytes). " +
                    $"Chave atual tem {Encoding.UTF8.GetByteCount(jwtSecretKey)} bytes. " +
                    "Configure uma chave secreta mais longa no appsettings.json.");
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

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

            var expirationMinutes = int.Parse(_configuration["Jwt:AccessTokenExpirationMinutes"] ?? "15");
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
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

        public async Task<LoginResponse?> RenovarTokenAsync(string refreshToken, CancellationToken ct)
        {
            _logger.LogInformation("Tentativa de renovação de token com refresh token");

            var storedToken = await _refreshTokenRepository.ObterPorTokenAsync(refreshToken, ct);
            if (storedToken == null)
            {
                _logger.LogWarning("Refresh token inválido ou expirado");
                return null;
            }

            var usuario = await _usuariosRepository.ObterPorIdAsync(storedToken.UsuarioId, ct);
            if (usuario == null || !usuario.Ativo || usuario.Excluido)
            {
                _logger.LogWarning("Usuário não encontrado ou inativo para refresh token. UsuarioId: {UsuarioId}", storedToken.UsuarioId);
                await _refreshTokenRepository.RevogarAsync(storedToken.RefreshTokenId, ct);
                return null;
            }

            await _refreshTokenRepository.RevogarAsync(storedToken.RefreshTokenId, ct);

            var newToken = GerarJwtToken(usuario);
            var newRefreshTokenString = GerarRefreshToken();
            
            var refreshTokenExpirationDays = int.Parse(_configuration["Jwt:RefreshTokenExpirationDays"] ?? "7");
            var newRefreshToken = new RefreshToken
            {
                UsuarioId = usuario.UsuarioId,
                Token = newRefreshTokenString,
                ExpiraEm = DateTime.UtcNow.AddDays(refreshTokenExpirationDays),
                Revogado = false,
                DataCriacao = DateTime.UtcNow
            };
            
            await _refreshTokenRepository.CriarAsync(newRefreshToken, ct);

            _logger.LogInformation("Token renovado com sucesso. UsuarioId: {UsuarioId}", usuario.UsuarioId);

            return new LoginResponse(
                newToken,
                newRefreshTokenString,
                usuario.UsuarioId,
                usuario.NomeCompleto,
                usuario.TenantId,
                (byte)usuario.TipoUsuarioId
            );
        }
    }
}

