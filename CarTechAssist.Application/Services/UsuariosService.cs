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

            // CORREÇÃO CRÍTICA: Validar se usuário pertence ao tenant
            if (usuario.TenantId != tenantId)
                throw new UnauthorizedAccessException("Usuário não pertence ao tenant atual.");

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
            _logger.LogInformation("🟢 SERVICE: Iniciando criação de usuário. TenantId: {TenantId}, Login: {Login}, TipoUsuarioId: {TipoUsuarioId}",
                tenantId, request.Login, request.TipoUsuarioId);

            try
            {
                // Validar login único
                _logger.LogInformation("🔍 Verificando se login já existe...");
                var existe = await _usuariosRepository.ExisteLoginAsync(tenantId, request.Login, ct);
                if (existe)
                {
                    _logger.LogWarning("⚠️ Login '{Login}' já está em uso no tenant {TenantId}", request.Login, tenantId);
                    throw new InvalidOperationException($"Login '{request.Login}' já está em uso neste tenant.");
                }
                _logger.LogInformation("✅ Login disponível.");

                // Hash da senha
                _logger.LogInformation("🔐 Gerando hash da senha...");
                var (hash, salt) = HashPassword(request.Senha);
                _logger.LogInformation("✅ Hash gerado. HashLength: {HashLength}, SaltLength: {SaltLength}",
                    hash?.Length ?? 0, salt?.Length ?? 0);

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
                    PrecisaTrocarSenha = false, // CORREÇÃO: Definir PrecisaTrocarSenha
                    Ativo = true,
                    DataCriacao = DateTime.UtcNow,
                    Excluido = false
                };

                _logger.LogInformation("📦 Objeto Usuario criado. Chamando repositório...");
                usuario = await _usuariosRepository.CriarAsync(usuario, ct);
                _logger.LogInformation("✅ Repositório retornou. UsuarioId: {UsuarioId}", usuario.UsuarioId);

                var dto = new UsuarioDto(
                    usuario.UsuarioId,
                    usuario.Login,
                    usuario.NomeCompleto,
                    usuario.Email,
                    usuario.Telefone,
                    (byte)usuario.TipoUsuarioId,
                    usuario.Ativo,
                    usuario.DataCriacao
                );

                _logger.LogInformation("🎉 SERVICE: Usuário criado com sucesso! UsuarioId: {UsuarioId}, Login: {Login}",
                    dto.UsuarioId, dto.Login);

                return dto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ERRO no SERVICE ao criar usuário. Tipo: {Type}, Message: {Message}",
                    ex.GetType().Name, ex.Message);
                throw;
            }
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

        public async Task AlterarAtivacaoAsync(int usuarioId, bool ativo, CancellationToken ct)
        {
            await _usuariosRepository.AlterarAtivacaoAsync(usuarioId, ativo, ct);
        }

        public async Task ResetSenhaAsync(int usuarioId, string novaSenha, CancellationToken ct)
        {
            var (hash, salt) = HashPassword(novaSenha);
            await _usuariosRepository.AtualizarSenhaAsync(usuarioId, hash, salt, ct);
        }

        public async Task ResetSenhaPorLoginAsync(int tenantId, string login, string novaSenha, CancellationToken ct)
        {
            // Buscar usuário pelo login
            var usuario = await _usuariosRepository.ObterPorLoginAsync(tenantId, login, ct);
            if (usuario == null)
                throw new InvalidOperationException($"Usuário com login '{login}' não encontrado no tenant {tenantId}.");

            // Resetar a senha
            var (hash, salt) = HashPassword(novaSenha);
            await _usuariosRepository.AtualizarSenhaAsync(usuario.UsuarioId, hash, salt, ct);
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