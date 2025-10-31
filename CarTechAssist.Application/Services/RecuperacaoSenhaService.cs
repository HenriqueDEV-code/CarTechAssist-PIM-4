using CarTechAssist.Domain.Interfaces;
using CarTechAssist.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace CarTechAssist.Application.Services
{
    public class RecuperacaoSenhaService
    {
        private readonly IRecuperacaoSenhaRepository _recuperacaoRepository;
        private readonly IUsuariosRepository _usuariosRepository;
        private readonly EmailService _emailService;
        private readonly ILogger<RecuperacaoSenhaService> _logger;

        public RecuperacaoSenhaService(
            IRecuperacaoSenhaRepository recuperacaoRepository,
            IUsuariosRepository usuariosRepository,
            EmailService emailService,
            ILogger<RecuperacaoSenhaService> logger)
        {
            _recuperacaoRepository = recuperacaoRepository;
            _usuariosRepository = usuariosRepository;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<bool> SolicitarRecuperacaoAsync(
            int tenantId,
            string login,
            string email,
            CancellationToken ct)
        {
            _logger.LogInformation("Solicitação de recuperação de senha. Tenant: {TenantId}, Login: {Login}, Email: {Email}", 
                tenantId, login, email);

            // Buscar usuário
            var usuario = await _usuariosRepository.ObterPorLoginAsync(tenantId, login, ct);
            if (usuario == null)
            {
                _logger.LogWarning("Tentativa de recuperação de senha para usuário inexistente. Login: {Login}, Tenant: {TenantId}", 
                    login, tenantId);
                // Por segurança, não revelar se o usuário existe ou não
                return true;
            }

            // Verificar se o email corresponde
            if (string.IsNullOrEmpty(usuario.Email) || 
                !usuario.Email.Equals(email, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Email não corresponde para usuário {UsuarioId}", usuario.UsuarioId);
                // Por segurança, retornar sucesso mesmo se email não corresponder
                return true;
            }

            // Limpar códigos expirados
            await _recuperacaoRepository.LimparExpiradasAsync(tenantId, ct);

            // Verificar se já existe código válido
            var codigoExistente = await _recuperacaoRepository.ObterPorUsuarioAsync(tenantId, usuario.UsuarioId, ct);
            if (codigoExistente != null)
            {
                // Reutilizar código existente
                _logger.LogInformation("Reutilizando código de recuperação existente para usuário {UsuarioId}", usuario.UsuarioId);
                
                var emailEnviado = await _emailService.EnviarCodigoRecuperacaoAsync(
                    usuario.Email,
                    usuario.NomeCompleto,
                    codigoExistente.Codigo,
                    ct);

                return emailEnviado;
            }

            // Gerar novo código
            var codigo = GerarCodigo();
            _logger.LogInformation("Código de recuperação gerado: {Codigo} para usuário {UsuarioId}", codigo, usuario.UsuarioId);

            var recuperacao = new RecuperacaoSenha
            {
                TenantId = tenantId,
                UsuarioId = usuario.UsuarioId,
                Codigo = codigo,
                Email = usuario.Email,
                DataExpiracao = DateTime.UtcNow.AddMinutes(30),
                Usado = false,
                DataCriacao = DateTime.UtcNow
            };

            var recuperacaoId = await _recuperacaoRepository.CriarAsync(recuperacao, ct);
            _logger.LogInformation("Código de recuperação salvo no banco. RecuperacaoSenhaId: {RecuperacaoId}, Código: {Codigo}", 
                recuperacaoId, codigo);

            // Enviar email
            _logger.LogInformation("Tentando enviar email de recuperação para {Email}", usuario.Email);
            var enviado = await _emailService.EnviarCodigoRecuperacaoAsync(
                usuario.Email,
                usuario.NomeCompleto,
                codigo,
                ct);

            _logger.LogInformation("Código de recuperação gerado para usuário {UsuarioId}. Email enviado: {Enviado}, Código: {Codigo}", 
                usuario.UsuarioId, enviado, codigo);

            return enviado;
        }

        public async Task<(bool UsuarioEncontrado, bool EmailEnviado, string? Codigo)> SolicitarRecuperacaoComDetalhesAsync(
            int tenantId,
            string login,
            string email,
            CancellationToken ct)
        {
            _logger.LogInformation("Solicitação de recuperação de senha. Tenant: {TenantId}, Login: {Login}, Email: {Email}", 
                tenantId, login, email);

            // Buscar usuário
            var usuario = await _usuariosRepository.ObterPorLoginAsync(tenantId, login, ct);
            if (usuario == null)
            {
                _logger.LogWarning("Tentativa de recuperação de senha para usuário inexistente. Login: {Login}, Tenant: {TenantId}", 
                    login, tenantId);
                return (false, false, null);
            }

            // Verificar se o email corresponde
            if (string.IsNullOrEmpty(usuario.Email) || 
                !usuario.Email.Equals(email, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Email não corresponde para usuário {UsuarioId}. Email cadastrado: {EmailCadastrado}, Email fornecido: {EmailFornecido}", 
                    usuario.UsuarioId, usuario.Email, email);
                return (true, false, null);
            }

            // Limpar códigos expirados
            await _recuperacaoRepository.LimparExpiradasAsync(tenantId, ct);

            // Verificar se já existe código válido
            var codigoExistente = await _recuperacaoRepository.ObterPorUsuarioAsync(tenantId, usuario.UsuarioId, ct);
            string codigo;
            if (codigoExistente != null)
            {
                // Reutilizar código existente
                codigo = codigoExistente.Codigo;
                _logger.LogInformation("Reutilizando código de recuperação existente para usuário {UsuarioId}", usuario.UsuarioId);
            }
            else
            {
                // Gerar novo código
                codigo = GerarCodigo();
                _logger.LogInformation("Código de recuperação gerado: {Codigo} para usuário {UsuarioId}", codigo, usuario.UsuarioId);

                var recuperacao = new RecuperacaoSenha
                {
                    TenantId = tenantId,
                    UsuarioId = usuario.UsuarioId,
                    Codigo = codigo,
                    Email = usuario.Email,
                    DataExpiracao = DateTime.UtcNow.AddMinutes(30),
                    Usado = false,
                    DataCriacao = DateTime.UtcNow
                };

                var recuperacaoId = await _recuperacaoRepository.CriarAsync(recuperacao, ct);
                _logger.LogInformation("Código de recuperação salvo no banco. RecuperacaoSenhaId: {RecuperacaoId}, Código: {Codigo}", 
                    recuperacaoId, codigo);
            }

            // Enviar email
            _logger.LogInformation("Tentando enviar email de recuperação para {Email}", usuario.Email);
            var enviado = await _emailService.EnviarCodigoRecuperacaoAsync(
                usuario.Email,
                usuario.NomeCompleto,
                codigo,
                ct);

            _logger.LogInformation("Resultado final - UsuarioId: {UsuarioId}, Email enviado: {Enviado}, Código: {Codigo}", 
                usuario.UsuarioId, enviado, codigo);

            return (true, enviado, codigo);
        }

        public async Task<bool> ValidarCodigoAsync(string codigo, CancellationToken ct)
        {
            var recuperacao = await _recuperacaoRepository.ObterPorCodigoAsync(codigo, ct);
            return recuperacao != null;
        }

        public async Task<bool> RedefinirSenhaAsync(
            string codigo,
            string novaSenha,
            CancellationToken ct)
        {
            _logger.LogInformation("Tentativa de redefinição de senha com código {Codigo}", codigo);

            var recuperacao = await _recuperacaoRepository.ObterPorCodigoAsync(codigo, ct);
            if (recuperacao == null)
            {
                _logger.LogWarning("Código de recuperação inválido ou expirado: {Codigo}", codigo);
                return false;
            }

            // Buscar usuário
            var usuario = await _usuariosRepository.ObterPorIdAsync(recuperacao.UsuarioId, ct);
            if (usuario == null)
            {
                _logger.LogWarning("Usuário não encontrado para recuperação {RecuperacaoSenhaId}", recuperacao.RecuperacaoSenhaId);
                return false;
            }

            // Validar senha
            if (string.IsNullOrWhiteSpace(novaSenha) || novaSenha.Length < 6)
            {
                _logger.LogWarning("Senha inválida na recuperação");
                return false;
            }

            // Atualizar senha
            using var hmac = new System.Security.Cryptography.HMACSHA512();
            var salt = hmac.Key;
            var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(novaSenha));

            await _usuariosRepository.AtualizarSenhaAsync(usuario.UsuarioId, hash, salt, ct);

            // Marcar código como usado
            await _recuperacaoRepository.MarcarComoUsadoAsync(recuperacao.RecuperacaoSenhaId, ct);

            _logger.LogInformation("Senha redefinida com sucesso para usuário {UsuarioId}", usuario.UsuarioId);

            return true;
        }

        private static string GerarCodigo()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }
    }
}

