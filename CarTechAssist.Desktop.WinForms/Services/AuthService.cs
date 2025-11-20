using CarTechAssist.Contracts.Auth;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CarTechAssist.Desktop.WinForms.Services
{
    public class AuthService
    {
        private readonly ApiClientService _apiClient;

        public AuthService(ApiClientService apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
        {
            // Login não precisa de autenticação, então criar um request sem headers de auth
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = null
            };
            
            var json = JsonSerializer.Serialize(request, jsonOptions);
            System.Diagnostics.Debug.WriteLine($"🔍 AuthService.LoginAsync - Request JSON: {json}");
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/auth/login") { Content = content };
            
            // Não adicionar headers de autenticação para login
            var response = await _apiClient.GetHttpClient().SendAsync(httpRequest, ct);
            
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            System.Diagnostics.Debug.WriteLine($"🔍 AuthService.LoginAsync - Response Status: {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"🔍 AuthService.LoginAsync - Response Content: {(string.IsNullOrEmpty(responseContent) ? "VAZIO" : responseContent.Substring(0, Math.Min(200, responseContent.Length)))}...");
            
            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"❌ AuthService.LoginAsync - Erro HTTP {response.StatusCode}: {responseContent}");
                throw new HttpRequestException($"Erro HTTP {response.StatusCode}: {responseContent}");
            }
            
            if (string.IsNullOrEmpty(responseContent))
            {
                System.Diagnostics.Debug.WriteLine($"❌ AuthService.LoginAsync - Response content está vazio");
                return null;
            }
            
            try
            {
                var loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseContent, jsonOptions);
                
                if (loginResponse != null)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ AuthService.LoginAsync - LoginResponse deserializado: Token={(!string.IsNullOrEmpty(loginResponse.Token) ? "OK" : "NULL")}, UsuarioId={loginResponse.UsuarioId}, TenantId={loginResponse.TenantId}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"❌ AuthService.LoginAsync - LoginResponse é NULL após deserialização");
                }
                
                return loginResponse;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ AuthService.LoginAsync - Erro ao deserializar: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ AuthService.LoginAsync - StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task<LoginResponse?> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
        {
            var request = new RefreshTokenRequest(refreshToken);
            return await _apiClient.PostAsync<LoginResponse>("api/auth/refresh", request, ct);
        }

        public async Task<UsuarioLogadoDto?> GetUsuarioLogadoAsync(CancellationToken ct = default)
        {
            return await _apiClient.GetAsync<UsuarioLogadoDto>("api/auth/me", ct);
        }
    }
}

