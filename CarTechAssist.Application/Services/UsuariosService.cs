using System.Security.Cryptography;
using System.Text;
using CarTechAssist.Contracts.Common;
using CarTechAssist.Contracts.Usuarios;
using CarTechAssist.Domain.Entities;
using CarTechAssist.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace CarTechAssist.Application.Services
{
    public class UsuariosService
    {
        private readonly IUsuariosRepository _usuariosRepository;
        private readonly ILogger<UsuariosService> _logger;

        public UsuariosService(IUsuariosRepository usuariosRepository, ILogger<UsuariosService> logger)
        {
            _usuariosRepository = usuariosRepository;
            _logger = logger;
        }

        public async Task<PagedResult<UsuarioDto>> ListarAsync(
            int tenantId,
            byte? tipoUsuarioId,
            bool? ativo,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            var (items, total) = await _usuariosRepository.ListarAsync(
                tenantId, tipoUsuarioId, ativo, page, pageSize, ct);

            var dtos = items.Select(u => new UsuarioDto(
                u.UsuarioId,
                u.Login,
                u.NomeCompleto,
                u.Email,
                u.Telefone,
                (byte)u.TipoUsuarioId,
                u.Ativo,
                u.DataCriacao
            )).ToList();

            return new PagedResult<UsuarioDto>(dtos, total, page, pageSize);
        }

        public async Task<UsuarioDto?> ObterAsync(int tenantId, int usuarioId, CancellationToken ct)
        {
            var usuario = await _usuariosRepository.ObterPorIdAsync(usuarioId, ct);
            if (usuario == null) return null;

            // Validação de segurança: verificar se o usuário pertence ao tenant
            if (usuario.TenantId != tenantId)
            {
                _logger.LogWarning("Tentativa de acesso a usuário de outro tenant. UsuarioId: {UsuarioId}, Tenant esperado: {TenantId}, Tenant do usuário: {UsuarioTenantId}",
                    usuarioId, tenantId, usuario.TenantId);
                return null;
            }

            return new UsuarioDto(
                usuario.UsuarioId,
                usuario.Login,
                usuario.NomeCompleto,
                usuario.Email,
                usuario.Telefone,
                (byte)usuario.TipoUsuarioId,
                usuario.Ativo,
                usuario.DataCriacao
            );
        }

        public async Task<UsuarioDto> CriarAsync(
            int tenantId,
            CriarUsuarioRequest request,
            CancellationToken ct)
        {
            _logger.LogInformation("Criando novo usuário para tenant {TenantId}: {Login}", tenantId, request.Login);
            
            // Validar login único
            var existe = await _usuariosRepository.ExisteLoginAsync(tenantId, request.Login, ct);
            if (existe)
            {
                _logger.LogWarning("Tentativa de criar usuário com login já existente. Tenant: {TenantId}, Login: {Login}", tenantId, request.Login);
                throw new InvalidOperationException($"Login '{request.Login}' já está em uso neste tenant.");
            }

            // Hash da senha
            var (hash, salt) = HashPassword(request.Senha);

            var usuario = new Usuario
            {
                TenantId = tenantId,
                Login = request.Login,
                NomeCompleto = request.NomeCompleto,
                Email = request.Email,
                Telefone = request.Telefone,
                TipoUsuarioId = (Domain.Enums.TipoUsuarios)request.TipoUsuarioId,
                HashSenha = hash,
                SaltSenha = salt,
                Ativo = true,
                DataCriacao = DateTime.UtcNow,
                Excluido = false
            };

            usuario = await _usuariosRepository.CriarAsync(usuario, ct);

            return new UsuarioDto(
                usuario.UsuarioId,
                usuario.Login,
                usuario.NomeCompleto,
                usuario.Email,
                usuario.Telefone,
                (byte)usuario.TipoUsuarioId,
                usuario.Ativo,
                usuario.DataCriacao
            );
        }

        public async Task<UsuarioDto> AtualizarAsync(
            int tenantId,
            int usuarioId,
            AtualizarUsuarioRequest request,
            CancellationToken ct)
        {
            var usuario = await _usuariosRepository.ObterPorIdAsync(usuarioId, ct);
            if (usuario == null)
                throw new InvalidOperationException($"Usuário {usuarioId} não encontrado.");

            if (usuario.TenantId != tenantId)
                throw new UnauthorizedAccessException("Usuário não pertence ao tenant atual.");

            usuario.NomeCompleto = request.NomeCompleto;
            usuario.Email = request.Email;
            usuario.Telefone = request.Telefone;
            usuario.TipoUsuarioId = (Domain.Enums.TipoUsuarios)request.TipoUsuarioId;
            usuario.DataAtualizacao = DateTime.UtcNow;

            usuario = await _usuariosRepository.AtualizarAsync(usuario, ct);

            return new UsuarioDto(
                usuario.UsuarioId,
                usuario.Login,
                usuario.NomeCompleto,
                usuario.Email,
                usuario.Telefone,
                (byte)usuario.TipoUsuarioId,
                usuario.Ativo,
                usuario.DataCriacao
            );
        }

        public async Task AlterarAtivacaoAsync(int tenantId, int usuarioId, bool ativo, CancellationToken ct)
        {
            // Validação de segurança: verificar se o usuário pertence ao tenant
            var usuario = await _usuariosRepository.ObterPorIdAsync(usuarioId, ct);
            if (usuario == null)
            {
                _logger.LogWarning("Tentativa de alterar ativação de usuário inexistente. UsuarioId: {UsuarioId}", usuarioId);
                throw new InvalidOperationException($"Usuário {usuarioId} não encontrado.");
            }

            if (usuario.TenantId != tenantId)
            {
                _logger.LogWarning("Tentativa de alterar ativação de usuário de outro tenant. UsuarioId: {UsuarioId}, Tenant esperado: {TenantId}, Tenant do usuário: {UsuarioTenantId}",
                    usuarioId, tenantId, usuario.TenantId);
                throw new UnauthorizedAccessException("Usuário não pertence ao tenant atual.");
            }

            await _usuariosRepository.AlterarAtivacaoAsync(usuarioId, ativo, ct);
        }

        public async Task ResetSenhaAsync(int tenantId, int usuarioId, string novaSenha, CancellationToken ct)
        {
            // Validação de segurança: verificar se o usuário pertence ao tenant
            var usuario = await _usuariosRepository.ObterPorIdAsync(usuarioId, ct);
            if (usuario == null)
            {
                _logger.LogWarning("Tentativa de reset de senha de usuário inexistente. UsuarioId: {UsuarioId}", usuarioId);
                throw new InvalidOperationException($"Usuário {usuarioId} não encontrado.");
            }

            if (usuario.TenantId != tenantId)
            {
                _logger.LogWarning("Tentativa de reset de senha de usuário de outro tenant. UsuarioId: {UsuarioId}, Tenant esperado: {TenantId}, Tenant do usuário: {UsuarioTenantId}",
                    usuarioId, tenantId, usuario.TenantId);
                throw new UnauthorizedAccessException("Usuário não pertence ao tenant atual.");
            }

            // Validação de força da senha
            if (string.IsNullOrWhiteSpace(novaSenha))
            {
                throw new ArgumentException("Senha não pode ser vazia.");
            }

            if (novaSenha.Length < 6)
            {
                throw new ArgumentException("Senha deve ter no mínimo 6 caracteres.");
            }

            if (novaSenha.Length > 100)
            {
                throw new ArgumentException("Senha deve ter no máximo 100 caracteres.");
            }

            // Validações adicionais de força (opcional, mas recomendado)
            if (novaSenha.All(char.IsDigit))
            {
                throw new ArgumentException("Senha não pode conter apenas números.");
            }

            if (novaSenha.All(char.IsLetter))
            {
                throw new ArgumentException("Senha deve conter pelo menos um número.");
            }

            // Verificar se não é uma senha muito comum (opcional)
            var senhasComuns = new[] { "123456", "senha", "password", "12345678", "qwerty" };
            if (senhasComuns.Contains(novaSenha.ToLowerInvariant()))
            {
                throw new ArgumentException("Senha muito comum. Escolha uma senha mais segura.");
            }

            var (hash, salt) = HashPassword(novaSenha);
            await _usuariosRepository.AtualizarSenhaAsync(usuarioId, hash, salt, ct);
            
            _logger.LogInformation("Senha resetada com sucesso para usuário {UsuarioId}", usuarioId);
        }

        public async Task ResetSenhaPorLoginAsync(int tenantId, string login, string novaSenha, CancellationToken ct)
        {
            var usuario = await _usuariosRepository.ObterPorLoginAsync(tenantId, login, ct);
            if (usuario == null)
            {
                _logger.LogWarning("Tentativa de reset de senha de usuário inexistente. Login: {Login}, Tenant: {TenantId}", login, tenantId);
                throw new InvalidOperationException($"Usuário com login '{login}' não encontrado.");
            }

            // Validação de força da senha
            if (string.IsNullOrWhiteSpace(novaSenha))
            {
                throw new ArgumentException("Senha não pode ser vazia.");
            }

            if (novaSenha.Length < 6)
            {
                throw new ArgumentException("Senha deve ter no mínimo 6 caracteres.");
            }

            // Hash da nova senha
            var (hash, salt) = HashPassword(novaSenha);

            await _usuariosRepository.AtualizarSenhaAsync(usuario.UsuarioId, hash, salt, ct);
            _logger.LogInformation("Senha resetada com sucesso. Login: {Login}, UsuarioId: {UsuarioId}", login, usuario.UsuarioId);
        }

        private static (byte[] hash, byte[] salt) HashPassword(string password)
        {
            using var hmac = new HMACSHA512();
            var salt = hmac.Key;
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            return (hash, salt);
        }
    }
}