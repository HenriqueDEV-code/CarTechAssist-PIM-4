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

            // VALIDAÇÃO CRÍTICA: Verificar se o email corresponde
            var emailCadastradoOriginal = usuario.Email ?? string.Empty;
            var emailInformadoOriginal = email ?? string.Empty;
            
            // Normalização completa
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
                    : $"O email informado não corresponde ao cadastrado. O email cadastrado para o login '{login}' é: {emailCadastradoOriginal}";
                
                throw new InvalidOperationException(mensagemErro);
            }

            _logger.LogInformation("✅ VALIDAÇÃO OK: Email corresponde para usuário {UsuarioId}", usuario.UsuarioId);

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

            // VALIDAÇÃO 1: Verificar se login foi informado
            if (string.IsNullOrWhiteSpace(login))
            {
                throw new ArgumentException("Login é obrigatório.");
            }

            // VALIDAÇÃO 2: Verificar se email foi informado
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email é obrigatório.");
            }

            // VALIDAÇÃO 3: Buscar usuário pelo login (trim no login também)
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

            // VALIDAÇÃO 4: Verificar se o usuário tem email cadastrado
            if (string.IsNullOrWhiteSpace(usuario.Email))
            {
                _logger.LogError("❌ VALIDAÇÃO FALHOU: Usuário {UsuarioId} não possui email cadastrado", usuario.UsuarioId);
                throw new InvalidOperationException($"O usuário '{loginTrimmed}' não possui email cadastrado no sistema. Entre em contato com o administrador.");
            }

            // VALIDAÇÃO 5: Normalizar emails para comparação rigorosa
            var emailCadastradoOriginal = usuario.Email ?? string.Empty;
            var emailInformadoOriginal = email ?? string.Empty;
            
            // Normalização completa: trim, lowercase, remover espaços extras
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

            // ═══════════════════════════════════════════════════════════════
            // VALIDAÇÃO CRÍTICA: Verificar se o email informado corresponde EXATAMENTE ao email cadastrado
            // ═══════════════════════════════════════════════════════════════
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
                
                // PARAR AQUI - Não processar mais nada!
                // Lançar exceção ANTES de qualquer processamento adicional (limpar códigos, gerar código, etc.)
                var mensagemErro = $"O email informado não corresponde ao cadastrado. O email cadastrado para o login '{loginTrimmed}' é: {emailCadastradoOriginal}";
                
                _logger.LogError("🚫🚫🚫 LANÇANDO EXCEÇÃO E PARANDO PROCESSAMENTO: {MensagemErro}", mensagemErro);
                
                throw new InvalidOperationException(mensagemErro);
            }

            _logger.LogInformation("✅✅✅ VALIDAÇÃO OK: Email corresponde perfeitamente!");
            _logger.LogInformation("  - UsuarioId: {UsuarioId}", usuario.UsuarioId);
            _logger.LogInformation("  - Login: '{Login}'", loginTrimmed);
            _logger.LogInformation("  - Email: '{Email}'", usuario.Email);

            // ═══════════════════════════════════════════════════════════════
            // VERIFICAÇÃO FINAL DE SEGURANÇA: Garantir que email ainda corresponde
            // ═══════════════════════════════════════════════════════════════
            // Verificação adicional antes de prosseguir (double-check)
            var verificacaoFinalCadastrado = (usuario.Email ?? string.Empty).Trim().ToLowerInvariant();
            var verificacaoFinalInformado = (email ?? string.Empty).Trim().ToLowerInvariant();
            
            if (!verificacaoFinalCadastrado.Equals(verificacaoFinalInformado, StringComparison.Ordinal))
            {
                _logger.LogError("🚨🚨🚨 FALHA NA VERIFICAÇÃO FINAL: Email ainda não corresponde!");
                throw new InvalidOperationException($"O email informado não corresponde ao cadastrado. O email cadastrado para o login '{loginTrimmed}' é: {usuario.Email ?? "N/A"}");
            }

            // ═══════════════════════════════════════════════════════════════
            // SÓ CHEGA AQUI SE A VALIDAÇÃO PASSOU 100%!
            // Agora pode prosseguir com geração do código
            // ═══════════════════════════════════════════════════════════════

            // ═══════════════════════════════════════════════════════════════
            // IMPORTANTE: Só chegou aqui se email corresponde 100%!
            // Agora pode limpar códigos antigos e gerar/enviar novo código
            // ═══════════════════════════════════════════════════════════════

            // Limpar códigos expirados
            await _recuperacaoRepository.LimparExpiradasAsync(tenantId, ct);

            // Verificar se já existe código válido (mas só se chegou até aqui = email válido)
            var codigoExistente = await _recuperacaoRepository.ObterPorUsuarioAsync(tenantId, usuario.UsuarioId, ct);
            string codigo;
            if (codigoExistente != null)
            {
                // VALIDAÇÃO ADICIONAL: Verificar se o email do código corresponde ao email cadastrado do usuário
                // Isso previne reutilização de códigos gerados com emails incorretos (antes da validação rigorosa)
                var emailCodigoNormalizado = (codigoExistente.Email ?? string.Empty).Trim().ToLowerInvariant();
                var emailUsuarioNormalizado = (usuario.Email ?? string.Empty).Trim().ToLowerInvariant();
                
                if (!emailCodigoNormalizado.Equals(emailUsuarioNormalizado, StringComparison.Ordinal))
                {
                    _logger.LogWarning("Código existente não corresponde ao email atual do usuário. Invalidando código antigo. UsuarioId: {UsuarioId}", usuario.UsuarioId);
                    // Não reutilizar código antigo - gerar novo
                    codigoExistente = null;
                }
            }
            
            if (codigoExistente != null)
            {
                // Reutilizar código existente (só se email está correto e corresponde ao email do código)
                codigo = codigoExistente.Codigo ?? throw new InvalidOperationException("Código existente inválido");
                _logger.LogInformation("Reutilizando código de recuperação existente para usuário {UsuarioId} - Email válido confirmado", usuario.UsuarioId);
            }
            else
            {
                // Gerar novo código (só chega aqui se email está correto)
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

            // Enviar email (só chega aqui se email está correto e válido)
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

