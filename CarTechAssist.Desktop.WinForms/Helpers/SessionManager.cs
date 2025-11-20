using System.Text.Json;

namespace CarTechAssist.Desktop.WinForms.Helpers
{
    public static class SessionManager
    {
        private static string ConfigFile => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CarTechAssist",
            "session.json"
        );

        public class SessionData
        {
            public string? Token { get; set; }
            public string? RefreshToken { get; set; }
            public int UsuarioId { get; set; }
            public int TenantId { get; set; }
            public string? NomeCompleto { get; set; }
            public byte TipoUsuarioId { get; set; }
        }

        public static void SaveSession(SessionData session)
        {
            try
            {
                if (session == null)
                {
                    System.Diagnostics.Debug.WriteLine("❌ SessionManager.SaveSession - Session é NULL!");
                    throw new ArgumentNullException(nameof(session), "Session não pode ser null");
                }

                if (string.IsNullOrEmpty(session.Token))
                {
                    System.Diagnostics.Debug.WriteLine("❌ SessionManager.SaveSession - Token é NULL ou vazio!");
                    throw new ArgumentException("Token não pode ser null ou vazio", nameof(session));
                }

                // Criar diretório se não existir
                var directory = Path.GetDirectoryName(ConfigFile);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    System.Diagnostics.Debug.WriteLine($"✅ SessionManager.SaveSession - Diretório criado: {directory}");
                }

                var json = JsonSerializer.Serialize(session, new JsonSerializerOptions 
                { 
                    WriteIndented = false 
                });
                
                System.Diagnostics.Debug.WriteLine($"🔍 SessionManager.SaveSession - Salvando sessão no arquivo: {ConfigFile}");
                System.Diagnostics.Debug.WriteLine($"🔍 SessionManager.SaveSession - Token: {(!string.IsNullOrEmpty(session.Token) ? "OK" : "NULL")}, TenantId: {session.TenantId}, UsuarioId: {session.UsuarioId}");
                System.Diagnostics.Debug.WriteLine($"🔍 SessionManager.SaveSession - JSON: {json.Substring(0, Math.Min(100, json.Length))}...");
                
                // Usar FileStream com Flush para garantir que seja escrito
                File.WriteAllText(ConfigFile, json);
                
                // Forçar flush do sistema de arquivos
                System.IO.File.SetAttributes(ConfigFile, FileAttributes.Normal);
                
                // Garantir que o arquivo foi escrito
                if (File.Exists(ConfigFile))
                {
                    var fileInfo = new FileInfo(ConfigFile);
                    System.Diagnostics.Debug.WriteLine($"✅ SessionManager.SaveSession - Arquivo salvo com sucesso. Tamanho: {fileInfo.Length} bytes");
                    
                    // Verificar se o conteúdo está correto
                    var testRead = File.ReadAllText(ConfigFile);
                    if (string.IsNullOrEmpty(testRead))
                    {
                        throw new IOException("Arquivo foi criado mas está vazio");
                    }
                    System.Diagnostics.Debug.WriteLine($"✅ SessionManager.SaveSession - Conteúdo verificado: {testRead.Substring(0, Math.Min(100, testRead.Length))}...");
                }
                else
                {
                    throw new IOException($"Arquivo não foi criado: {ConfigFile}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ SessionManager.SaveSession - Erro ao salvar sessão: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ SessionManager.SaveSession - StackTrace: {ex.StackTrace}");
                throw; // Re-throw para que o erro seja visível
            }
        }

        public static SessionData? LoadSession()
        {
            try
            {
                if (!File.Exists(ConfigFile))
                {
                    System.Diagnostics.Debug.WriteLine($"🔍 SessionManager.LoadSession - Arquivo não existe: {ConfigFile}");
                    return null;
                }
                
                var json = File.ReadAllText(ConfigFile);
                if (string.IsNullOrEmpty(json))
                {
                    System.Diagnostics.Debug.WriteLine($"❌ SessionManager.LoadSession - Arquivo está vazio");
                    return null;
                }
                
                var session = JsonSerializer.Deserialize<SessionData>(json);
                if (session != null)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ SessionManager.LoadSession - Sessão carregada: Token={(!string.IsNullOrEmpty(session.Token) ? "OK" : "NULL")}, TenantId={session.TenantId}, UsuarioId={session.UsuarioId}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"❌ SessionManager.LoadSession - Falha ao deserializar sessão");
                }
                
                return session;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ SessionManager.LoadSession - Erro ao carregar sessão: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ SessionManager.LoadSession - StackTrace: {ex.StackTrace}");
                return null;
            }
        }

        public static void ClearSession()
        {
            try
            {
                if (File.Exists(ConfigFile))
                    File.Delete(ConfigFile);
            }
            catch { }
        }
    }
}

