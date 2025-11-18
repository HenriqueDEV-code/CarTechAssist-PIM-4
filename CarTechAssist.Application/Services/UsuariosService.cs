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

        
        public async Task<UsuarioDto?> ObterAsync(int tenantId, int usuarioId, CancellationToken ct)
        {
            var usuario = await _usuariosRepository.ObterPorIdAsync(usuarioId, ct);
            if (usuario == null) return null;

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

            var existe = await _usuariosRepository.ExisteLoginAsync(tenantId, request.Login, ct);
            if (existe)
                throw new InvalidOperationException($"Login '{request.Login}' já está em uso neste tenant.");

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
                PrecisaTrocarSenha = false,
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

        public async Task<UsuarioDto> AlterarAtivacaoAsync(int tenantId, int usuarioId, bool ativo, CancellationToken ct)
        {
            // Primeiro, verificar se o usuário existe e pertence ao tenant
            var usuarioAntes = await _usuariosRepository.ObterPorIdAsync(usuarioId, ct);
            if (usuarioAntes == null)
                throw new InvalidOperationException($"Usuário com ID {usuarioId} não encontrado.");
            
            if (usuarioAntes.TenantId != tenantId)
                throw new UnauthorizedAccessException("Usuário não pertence ao tenant atual.");
            
            System.Diagnostics.Debug.WriteLine($"[AlterarAtivacaoAsync] Antes: UsuarioId={usuarioId}, AtivoAtual={usuarioAntes.Ativo}, NovoAtivo={ativo}");
            
            // Agora atualizar
            await _usuariosRepository.AlterarAtivacaoAsync(usuarioId, ativo, ct);
            
            // Buscar o usuário atualizado para retornar
            var usuario = await _usuariosRepository.ObterPorIdAsync(usuarioId, ct);
            if (usuario == null)
                throw new InvalidOperationException($"Usuário com ID {usuarioId} não encontrado após atualização.");
            
            System.Diagnostics.Debug.WriteLine($"[AlterarAtivacaoAsync] Depois: UsuarioId={usuarioId}, Ativo={usuario.Ativo}");
            
            // Verificar se a atualização realmente funcionou
            if (usuario.Ativo != ativo)
            {
                throw new InvalidOperationException($"Falha ao atualizar status. Esperado: {ativo}, Obtido: {usuario.Ativo}");
            }
            
            // Verificar novamente o tenant (por segurança)
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

        public async Task ResetSenhaAsync(int usuarioId, string novaSenha, CancellationToken ct)
        {
            var (hash, salt) = HashPassword(novaSenha);
            await _usuariosRepository.AtualizarSenhaAsync(usuarioId, hash, salt, ct);
        }

        public async Task ResetSenhaPorLoginAsync(int tenantId, string login, string novaSenha, CancellationToken ct)
        {

            var usuario = await _usuariosRepository.ObterPorLoginAsync(tenantId, login, ct);
            if (usuario == null)
                throw new InvalidOperationException($"Usuário com login '{login}' não encontrado no tenant {tenantId}.");

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