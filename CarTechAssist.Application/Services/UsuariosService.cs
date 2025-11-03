using System.Security.Cryptography;
using System.Text;
using CarTechAssist.Contracts.Common;
using CarTechAssist.Contracts.Usuarios;
using CarTechAssist.Domain.Entities;
using CarTechAssist.Domain.Interfaces;

namespace CarTechAssist.Application.Services
{
    public class UsuariosService
    {
        private readonly IUsuariosRepository _usuariosRepository;

        public UsuariosService(IUsuariosRepository usuariosRepository)
        {
            _usuariosRepository = usuariosRepository;
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

        public async Task<UsuarioDto?> ObterAsync(int usuarioId, CancellationToken ct)
        {
            var usuario = await _usuariosRepository.ObterPorIdAsync(usuarioId, ct);
            if (usuario == null) return null;

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
            // Validar login único
            var existe = await _usuariosRepository.ExisteLoginAsync(tenantId, request.Login, ct);
            if (existe)
                throw new InvalidOperationException($"Login '{request.Login}' já está em uso neste tenant.");

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