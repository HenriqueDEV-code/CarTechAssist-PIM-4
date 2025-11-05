using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CarTechAssist.Application.Services
{
    public class EmailService
    {
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _smtpUser;
        private readonly string _smtpPass;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _logger = logger;
            
            // Configuração via appsettings.json
            _smtpServer = configuration["Email:SmtpServer"] ?? "smtp.gmail.com";
            _smtpPort = int.Parse(configuration["Email:SmtpPort"] ?? "587");
            _smtpUser = configuration["Email:SmtpUser"] ?? throw new InvalidOperationException("Email:SmtpUser não configurado no appsettings.json");
            _smtpPass = configuration["Email:SmtpPassword"] ?? throw new InvalidOperationException("Email:SmtpPassword não configurado no appsettings.json");
            
            _logger.LogInformation("EmailService configurado - Servidor: {Server}:{Port}, User: {User}", 
                _smtpServer, _smtpPort, _smtpUser);
        }

       
        private async Task<(bool Sucesso, string? ErroDetalhado)> EnviarEmailInternoAsync(
            string destinatario,
            string assunto,
            string corpo,
            bool isHtml,
            CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("🔵 ===== INÍCIO ENVIO DE EMAIL =====");
                _logger.LogInformation("🔵 Servidor: {Servidor}:{Porta}", _smtpServer, _smtpPort);
                _logger.LogInformation("🔵 De: {De}", _smtpUser);
                _logger.LogInformation("🔵 Para: {Para}", destinatario);
                _logger.LogInformation("🔵 Assunto: {Assunto}", assunto);
                _logger.LogInformation("🔵 App Password (primeiros 4 chars): {PrimeirosChars}...", 
                    _smtpPass.Length >= 4 ? _smtpPass.Substring(0, 4) : "****");

                // Validar email do destinatário
                if (string.IsNullOrWhiteSpace(destinatario) || !destinatario.Contains("@"))
                {
                    var erro = $"Email destinatário inválido: {destinatario}";
                    _logger.LogError("❌ {Erro}", erro);
                    return (false, erro);
                }

                // Configurar SMTP Client
                using var smtpClient = new SmtpClient(_smtpServer, _smtpPort)
                {
                    EnableSsl = true,
                    UseDefaultCredentials = false,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 60000
                };

                _logger.LogInformation("🔵 Configurando credenciais. User: {User}, Pass Length: {PassLength}", 
                    _smtpUser, _smtpPass?.Length ?? 0);
                
                smtpClient.Credentials = new NetworkCredential(_smtpUser, _smtpPass);
                
                _logger.LogInformation("🔵 SMTP Client configurado:");
                _logger.LogInformation("   Host: {Host}", smtpClient.Host);
                _logger.LogInformation("   Port: {Port}", smtpClient.Port);
                _logger.LogInformation("   SSL: {SSL}", smtpClient.EnableSsl);
                _logger.LogInformation("   Timeout: {Timeout}ms", smtpClient.Timeout);

                using var mensagem = new MailMessage
                {
                    From = new MailAddress(_smtpUser, "CarTechAssist"),
                    Subject = assunto,
                    Body = corpo,
                    IsBodyHtml = isHtml
                };

                var emailDestino = new MailAddress(destinatario);
                mensagem.To.Add(emailDestino);
                
                _logger.LogInformation("🔵 Mensagem criada:");
                _logger.LogInformation("   From: {From} ({FromAddress})", mensagem.From.DisplayName, mensagem.From.Address);
                _logger.LogInformation("   To: {To} ({ToAddress})", emailDestino.DisplayName, emailDestino.Address);
                _logger.LogInformation("   Subject: {Subject}", mensagem.Subject);

                _logger.LogInformation("🔵 ===== TENTANDO ENVIAR =====");
                _logger.LogInformation("🔵 Conectando ao servidor {Servidor}:{Porta}...", _smtpServer, _smtpPort);
                
                await smtpClient.SendMailAsync(mensagem, ct);
                
                _logger.LogInformation("✅ ===== EMAIL ENVIADO COM SUCESSO =====");
                return (true, null);
            }
            catch (SmtpException ex)
            {
                var erro = $"SmtpException - StatusCode: {ex.StatusCode}, Message: {ex.Message}";
                if (ex.InnerException != null)
                {
                    erro += $" | Inner: {ex.InnerException.Message}";
                }
                _logger.LogError("❌ ===== ERRO SMTP DETALHADO =====");
                _logger.LogError("❌ {Erro}", erro);
                
                if (ex.Message.Contains("Authentication") || ex.Message.Contains("535") || ex.Message.Contains("534"))
                {
                    _logger.LogError("❌ PROBLEMA: Autenticação falhou! Verifique:");
                    _logger.LogError("   1. App Password está correto? (16 caracteres, sem espaços)");
                    _logger.LogError("   2. Conta tem 2FA ativado?");
                    _logger.LogError("   3. App Password foi gerado recentemente?");
                }
                else if (ex.Message.Contains("Connection") || ex.Message.Contains("timeout") || ex.Message.Contains("refused"))
                {
                    _logger.LogError("❌ PROBLEMA: Falha de conexão! Verifique firewall, internet ou servidor acessível");
                }
                
                return (false, erro);
            }
            catch (System.Security.Authentication.AuthenticationException ex)
            {
                var erro = $"AuthenticationException: {ex.Message}";
                _logger.LogError("❌ ===== ERRO DE AUTENTICAÇÃO =====");
                _logger.LogError("❌ {Erro}", erro);
                return (false, erro);
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                var erro = $"SocketException - ErrorCode: {ex.SocketErrorCode}, Message: {ex.Message}";
                _logger.LogError("❌ ===== ERRO DE CONEXÃO DE REDE =====");
                _logger.LogError("❌ {Erro}", erro);
                return (false, erro);
            }
            catch (Exception ex)
            {
                var erro = $"{ex.GetType().Name}: {ex.Message}";
                if (ex.InnerException != null)
                {
                    erro += $" | Inner: {ex.InnerException.Message}";
                }
                _logger.LogError("❌ ===== ERRO GENÉRICO =====");
                _logger.LogError("❌ {Erro}", erro);
                return (false, erro);
            }
            finally
            {
                _logger.LogInformation("🔵 ===== FIM TENTATIVA DE ENVIO =====");
            }
        }

        public async Task<(bool Sucesso, string? ErroDetalhado)> EnviarEmailComDetalhesAsync(
            string destinatario,
            string assunto,
            string corpo,
            bool isHtml = true,
            CancellationToken ct = default)
        {
            return await EnviarEmailInternoAsync(destinatario, assunto, corpo, isHtml, ct);
        }

        public async Task<bool> EnviarEmailAsync(
            string destinatario,
            string assunto,
            string corpo,
            bool isHtml = true,
            CancellationToken ct = default)
        {
            var (sucesso, _) = await EnviarEmailInternoAsync(destinatario, assunto, corpo, isHtml, ct);
            return sucesso;
        }

        public async Task<bool> EnviarCodigoRecuperacaoAsync(
            string email,
            string nome,
            string codigo,
            CancellationToken ct = default)
        {
            _logger.LogInformation("📧 Preparando envio de email de recuperação para {Email} com código {Codigo}", email, codigo);
            
            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.LogError("❌ Email destinatário está vazio!");
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(codigo))
            {
                _logger.LogError("❌ Código está vazio!");
                return false;
            }
            
            var assunto = "Recuperação de Senha - CarTechAssist";
            var corpo = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{
            font-family: Arial, sans-serif;
            line-height: 1.6;
            color: #333;
        }}
        .container {{
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f4f4f4;
        }}
        .header {{
            background-color: #4CAF50;
            color: white;
            padding: 20px;
            text-align: center;
            border-radius: 5px 5px 0 0;
        }}
        .content {{
            background-color: white;
            padding: 30px;
            border-radius: 0 0 5px 5px;
        }}
        .codigo {{
            background-color: #282c34;
            color: #4CAF50;
            font-size: 32px;
            font-weight: bold;
            text-align: center;
            padding: 20px;
            margin: 20px 0;
            border-radius: 5px;
            letter-spacing: 5px;
        }}
        .footer {{
            text-align: center;
            color: #666;
            font-size: 12px;
            margin-top: 20px;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>CarTechAssist</h1>
        </div>
        <div class='content'>
            <p>Olá, <strong>{nome}</strong>!</p>
            <p>Você solicitou a recuperação de senha da sua conta.</p>
            <p>Use o código abaixo para redefinir sua senha:</p>
            <div class='codigo'>{codigo}</div>
            <p>Este código é válido por <strong>30 minutos</strong>.</p>
            <p>Se você não solicitou esta recuperação de senha, ignore este email.</p>
            <div class='footer'>
                <p>Este é um email automático, por favor não responda.</p>
                <p>&copy; 2025 CarTechAssist - Sistema de Gerenciamento de Chamados</p>
            </div>
        </div>
    </div>
</body>
</html>";

            return await EnviarEmailAsync(email, assunto, corpo, true, ct);
        }
    }
}

