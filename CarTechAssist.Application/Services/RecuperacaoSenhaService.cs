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

            var usuario = await _usuariosRepository.ObterPorLoginAsync(tenantId, login, ct);
            if (usuario == null)
            {
                _logger.LogWarning("Tentativa de recuperação de senha para usuário inexistente. Login: {Login}, Tenant: {TenantId}", 
                    login, tenantId);

                return true;
            }

            var emailCadastradoOriginal = usuario.Email ?? string.Empty;
            var emailInformadoOriginal = email ?? string.Empty;

            var emailCadastradoNormalizado = emailCadastradoOriginal
                .Trim()
                .ToLowerInvariant()
                .Replace(" ", "")
                .Replace("\t", "")
                .Replace("\n", "")
                .Replace("\r", "");
            
            var emailInformadoNormalizado = emailInformadoOriginal
                .Trim()
                .ToLowerInvariant()
                .Replace(" ", "")
                .Replace("\t", "")
                .Replace("\n", "")
                .Replace("\r", "");
            
            bool emailsCorrespondem = !string.IsNullOrWhiteSpace(emailCadastradoNormalizado) && 
                                     !string.IsNullOrWhiteSpace(emailInformadoNormalizado) &&
                                     emailCadastradoNormalizado.Equals(emailInformadoNormalizado, StringComparison.Ordinal);
            
            if (!emailsCorrespondem)
            {
                _logger.LogError("❌ VALIDAÇÃO FALHOU: Email não corresponde para usuário {UsuarioId}", usuario.UsuarioId);
                _logger.LogError("  - Login: '{Login}'", login);
                _logger.LogError("  - Email cadastrado: '{EmailCadastrado}'", emailCadastradoOriginal);
                _logger.LogError("  - Email fornecido: '{EmailFornecido}'", emailInformadoOriginal);

                var mensagemErro = string.IsNullOrWhiteSpace(emailCadastradoOriginal)
                    ? "Nenhum email cadastrado para este usuário."
                    : "O email informado não corresponde ao cadastrado.";
                
                throw new InvalidOperationException(mensagemErro);
            }

            _logger.LogInformation("✅ VALIDAÇÃO OK: Email corresponde para usuário {UsuarioId}", usuario.UsuarioId);

            var emailConfirmado = usuario.Email;
            if (string.IsNullOrWhiteSpace(emailConfirmado))
            {
                _logger.LogError("Email do usuário {UsuarioId} está nulo ou vazio após a validação", usuario.UsuarioId);
                throw new InvalidOperationException("Não foi possível confirmar o email do usuário.");
            }

            await _recuperacaoRepository.LimparExpiradasAsync(tenantId, ct);

            var codigoExistente = await _recuperacaoRepository.ObterPorUsuarioAsync(tenantId, usuario.UsuarioId, ct);
            if (codigoExistente != null)
            {

                _logger.LogInformation("Reutilizando código de recuperação existente para usuário {UsuarioId}", usuario.UsuarioId);
                
                var emailEnviado = await _emailService.EnviarCodigoRecuperacaoAsync(
                    emailConfirmado,
                    usuario.NomeCompleto,
                    codigoExistente.Codigo,
                    ct);

                return emailEnviado;
            }

            var codigo = GerarCodigo();
            _logger.LogInformation("Código de recuperação gerado: {Codigo} para usuário {UsuarioId}", codigo, usuario.UsuarioId);

            var recuperacao = new RecuperacaoSenha
            {
                TenantId = tenantId,
                UsuarioId = usuario.UsuarioId,
                Codigo = codigo,
                Email = emailConfirmado,
                DataExpiracao = DateTime.UtcNow.AddMinutes(30),
                Usado = false,
                DataCriacao = DateTime.UtcNow
            };

            var recuperacaoId = await _recuperacaoRepository.CriarAsync(recuperacao, ct);
            _logger.LogInformation("Código de recuperação salvo no banco. RecuperacaoSenhaId: {RecuperacaoId}, Código: {Codigo}", 
                recuperacaoId, codigo);

            _logger.LogInformation("Tentando enviar email de recuperação para {Email}", emailConfirmado);
            var enviado = await _emailService.EnviarCodigoRecuperacaoAsync(
                emailConfirmado,
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

            if (string.IsNullOrWhiteSpace(login))
            {
                throw new ArgumentException("Login é obrigatório.");
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email é obrigatório.");
            }

            var loginTrimmed = login.Trim();
            var usuario = await _usuariosRepository.ObterPorLoginAsync(tenantId, loginTrimmed, ct);
            
            if (usuario == null)
            {
                _logger.LogError("❌ VALIDAÇÃO FALHOU: Usuário não encontrado. Login: '{Login}', Tenant: {TenantId}", 
                    loginTrimmed, tenantId);
                throw new InvalidOperationException($"Usuário com login '{loginTrimmed}' não encontrado.");
            }

            _logger.LogInformation("✅ Usuário encontrado: ID={UsuarioId}, Login='{Login}', Email='{Email}'", 
                usuario.UsuarioId, usuario.Login, usuario.Email);

            if (string.IsNullOrWhiteSpace(usuario.Email))
            {
                _logger.LogError("❌ VALIDAÇÃO FALHOU: Usuário {UsuarioId} não possui email cadastrado", usuario.UsuarioId);
                throw new InvalidOperationException($"O usuário '{loginTrimmed}' não possui email cadastrado no sistema. Entre em contato com o administrador.");
            }

            var emailCadastradoOriginal = usuario.Email ?? string.Empty;
            var emailInformadoOriginal = email ?? string.Empty;

            var emailCadastradoNormalizado = emailCadastradoOriginal
                .Trim()
                .ToLowerInvariant()
                .Replace(" ", "")  // Remove espaços no meio
                .Replace("\t", "")  // Remove tabs
                .Replace("\n", "")  // Remove quebras de linha
                .Replace("\r", ""); // Remove carriage return
            
            var emailInformadoNormalizado = emailInformadoOriginal
                .Trim()
                .ToLowerInvariant()
                .Replace(" ", "")
                .Replace("\t", "")
                .Replace("\n", "")
                .Replace("\r", "");
            
            _logger.LogInformation("🔍 VALIDAÇÃO DE EMAIL:");
            _logger.LogInformation("  - Login: '{Login}'", loginTrimmed);
            _logger.LogInformation("  - Email cadastrado ORIGINAL: '{EmailCadastrado}'", emailCadastradoOriginal);
            _logger.LogInformation("  - Email informado ORIGINAL: '{EmailInformado}'", emailInformadoOriginal);
            _logger.LogInformation("  - Email cadastrado NORMALIZADO: '{EmailCadastradoNorm}'", emailCadastradoNormalizado);
            _logger.LogInformation("  - Email informado NORMALIZADO: '{EmailInformadoNorm}'", emailInformadoNormalizado);



            bool emailsCorrespondem = !string.IsNullOrWhiteSpace(emailCadastradoNormalizado) && 
                                     !string.IsNullOrWhiteSpace(emailInformadoNormalizado) &&
                                     emailCadastradoNormalizado.Equals(emailInformadoNormalizado, StringComparison.Ordinal);
            
            if (!emailsCorrespondem)
            {
                _logger.LogError("❌❌❌ VALIDAÇÃO FALHOU: Email não corresponde!");
                _logger.LogError("  - UsuarioId: {UsuarioId}", usuario.UsuarioId);
                _logger.LogError("  - Login: '{Login}'", loginTrimmed);
                _logger.LogError("  - Email cadastrado ORIGINAL: '{EmailCadastrado}'", emailCadastradoOriginal);
                _logger.LogError("  - Email fornecido ORIGINAL: '{EmailFornecido}'", emailInformadoOriginal);
                _logger.LogError("  - Email cadastrado NORMALIZADO: '{EmailCadastradoNorm}'", emailCadastradoNormalizado);
                _logger.LogError("  - Email fornecido NORMALIZADO: '{EmailFornecidoNorm}'", emailInformadoNormalizado);
                _logger.LogError("  - COMPARAÇÃO: '{EmailCadastradoNorm}' == '{EmailFornecidoNorm}' = {Resultado}", 
                    emailCadastradoNormalizado, emailInformadoNormalizado, emailsCorrespondem);



                var mensagemErro = "O email informado não corresponde ao cadastrado.";
                
                _logger.LogError("🚫🚫🚫 LANÇANDO EXCEÇÃO E PARANDO PROCESSAMENTO: Email não corresponde para login '{Login}'", loginTrimmed);
                
                throw new InvalidOperationException(mensagemErro);
            }

            _logger.LogInformation("✅✅✅ VALIDAÇÃO OK: Email corresponde perfeitamente!");
            _logger.LogInformation("  - UsuarioId: {UsuarioId}", usuario.UsuarioId);
            _logger.LogInformation("  - Login: '{Login}'", loginTrimmed);
            _logger.LogInformation("  - Email: '{Email}'", usuario.Email);




            var verificacaoFinalCadastrado = (usuario.Email ?? string.Empty).Trim().ToLowerInvariant();
            var verificacaoFinalInformado = (email ?? string.Empty).Trim().ToLowerInvariant();
            
            if (!verificacaoFinalCadastrado.Equals(verificacaoFinalInformado, StringComparison.Ordinal))
            {
                _logger.LogError("🚨🚨🚨 FALHA NA VERIFICAÇÃO FINAL: Email ainda não corresponde!");

                throw new InvalidOperationException("O email informado não corresponde ao cadastrado.");
            }









            await _recuperacaoRepository.LimparExpiradasAsync(tenantId, ct);

            var codigoExistente = await _recuperacaoRepository.ObterPorUsuarioAsync(tenantId, usuario.UsuarioId, ct);
            string codigo;
            if (codigoExistente != null)
            {


                var emailCodigoNormalizado = (codigoExistente.Email ?? string.Empty).Trim().ToLowerInvariant();
                var emailUsuarioNormalizado = (usuario.Email ?? string.Empty).Trim().ToLowerInvariant();
                
                if (!emailCodigoNormalizado.Equals(emailUsuarioNormalizado, StringComparison.Ordinal))
                {
                    _logger.LogWarning("Código existente não corresponde ao email atual do usuário. Invalidando código antigo. UsuarioId: {UsuarioId}", usuario.UsuarioId);

                    codigoExistente = null;
                }
            }
            
            if (codigoExistente != null)
            {

                codigo = codigoExistente.Codigo ?? throw new InvalidOperationException("Código existente inválido");
                _logger.LogInformation("Reutilizando código de recuperação existente para usuário {UsuarioId} - Email válido confirmado", usuario.UsuarioId);
            }
            else
            {

                codigo = GerarCodigo();
                _logger.LogInformation("Código de recuperação gerado: {Codigo} para usuário {UsuarioId} - Email válido confirmado", codigo, usuario.UsuarioId);

                var recuperacao = new RecuperacaoSenha
                {
                    TenantId = tenantId,
                    UsuarioId = usuario.UsuarioId,
                    Codigo = codigo,
                    Email = usuario.Email ?? throw new InvalidOperationException("Email não pode ser null neste ponto"),
                    DataExpiracao = DateTime.UtcNow.AddMinutes(30),
                    Usado = false,
                    DataCriacao = DateTime.UtcNow
                };

                var recuperacaoId = await _recuperacaoRepository.CriarAsync(recuperacao, ct);
                _logger.LogInformation("Código de recuperação salvo no banco. RecuperacaoSenhaId: {RecuperacaoId}, Código: {Codigo}", 
                    recuperacaoId, codigo);
            }

            var emailParaEnviar = usuario.Email ?? throw new InvalidOperationException("Email não pode ser null para envio");
            _logger.LogInformation("Tentando enviar email de recuperação para {Email} - Email válido confirmado", emailParaEnviar);
            var enviado = await _emailService.EnviarCodigoRecuperacaoAsync(
                emailParaEnviar,
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

            var usuario = await _usuariosRepository.ObterPorIdAsync(recuperacao.UsuarioId, ct);
            if (usuario == null)
            {
                _logger.LogWarning("Usuário não encontrado para recuperação {RecuperacaoSenhaId}", recuperacao.RecuperacaoSenhaId);
                return false;
            }

            if (string.IsNullOrWhiteSpace(novaSenha) || novaSenha.Length < 6)
            {
                _logger.LogWarning("Senha inválida na recuperação");
                return false;
            }

            using var hmac = new System.Security.Cryptography.HMACSHA512();
            var salt = hmac.Key;
            var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(novaSenha));

            await _usuariosRepository.AtualizarSenhaAsync(usuario.UsuarioId, hash, salt, ct);

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

