using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CarTechAssist.Contracts.Auth;
using CarTechAssist.Desktop.WinForms.Helpers;

namespace CarTechAssist.Desktop.WinForms.Services
{
    public class ApiClientService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private string? _token;
        private int _tenantId;
        private int _usuarioId;

        public ApiClientService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:5167"),
                Timeout = TimeSpan.FromSeconds(30)
            };

            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = null
            };
        }

        public void SetAuth(string token, int tenantId, int usuarioId)
        {
            _token = token;
            _tenantId = tenantId;
            _usuarioId = usuarioId;
            
            System.Diagnostics.Debug.WriteLine($"✅ SetAuth chamado - Token: {(!string.IsNullOrEmpty(token) ? "OK" : "NULL")}, TenantId: {tenantId}, UsuarioId: {usuarioId}");
        }

        public void ClearAuth()
        {
            _token = null;
            _tenantId = 0;
            _usuarioId = 0;
        }

        private void SetHeaders(HttpRequestMessage request)
        {
            // PRIMEIRO: Tentar usar os valores já configurados via SetAuth
            // Se não houver, carregar da sessão
            if (string.IsNullOrEmpty(_token) || _tenantId == 0 || _usuarioId == 0)
            {
                var session = SessionManager.LoadSession();
                if (session != null && !string.IsNullOrEmpty(session.Token))
                {
                    _token = session.Token;
                    _tenantId = session.TenantId;
                    _usuarioId = session.UsuarioId;
                    
                    System.Diagnostics.Debug.WriteLine($"✅ SetHeaders - Sessão carregada: Token={(!string.IsNullOrEmpty(_token) ? "OK" : "NULL")}, TenantId={_tenantId}, UsuarioId={_usuarioId}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"❌ SetHeaders - Sessão não encontrada ou token vazio");
                    _token = null;
                    _tenantId = 0;
                    _usuarioId = 0;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"✅ SetHeaders - Usando valores do SetAuth: Token=OK, TenantId={_tenantId}, UsuarioId={_usuarioId}");
            }

            // Remover headers existentes antes de adicionar novos
            try
            {
                request.Headers.Remove("Authorization");
            }
            catch { }
            
            try
            {
                request.Headers.Remove("X-Tenant-Id");
            }
            catch { }
            
            try
            {
                request.Headers.Remove("X-Usuario-Id");
            }
            catch { }

            if (!string.IsNullOrEmpty(_token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
                System.Diagnostics.Debug.WriteLine($"✅ Authorization header adicionado: Bearer {_token.Substring(0, Math.Min(20, _token.Length))}...");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"❌ Token vazio - Authorization header NÃO adicionado");
            }

            if (_tenantId > 0)
            {
                request.Headers.Add("X-Tenant-Id", _tenantId.ToString());
                System.Diagnostics.Debug.WriteLine($"✅ X-Tenant-Id header adicionado: {_tenantId}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"❌ TenantId inválido ({_tenantId}) - X-Tenant-Id header NÃO adicionado");
            }

            if (_usuarioId > 0)
            {
                request.Headers.Add("X-Usuario-Id", _usuarioId.ToString());
                System.Diagnostics.Debug.WriteLine($"✅ X-Usuario-Id header adicionado: {_usuarioId}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"❌ UsuarioId inválido ({_usuarioId}) - X-Usuario-Id header NÃO adicionado");
            }
        }

        public async Task<T?> GetAsync<T>(string endpoint, CancellationToken ct = default)
        {
            System.Diagnostics.Debug.WriteLine($"🔍 GET {endpoint}");
            
            // Verificar autenticação antes de fazer a requisição
            System.Diagnostics.Debug.WriteLine($"🔍 GetAsync - Antes de SetHeaders: _token={(!string.IsNullOrEmpty(_token) ? "OK" : "NULL")}, _tenantId={_tenantId}, _usuarioId={_usuarioId}");
            
            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            SetHeaders(request);
            
            // Verificar se os headers foram adicionados
            var authHeader = request.Headers.Authorization;
            var tenantHeader = request.Headers.Contains("X-Tenant-Id") ? request.Headers.GetValues("X-Tenant-Id").FirstOrDefault() : null;
            var usuarioHeader = request.Headers.Contains("X-Usuario-Id") ? request.Headers.GetValues("X-Usuario-Id").FirstOrDefault() : null;
            var authStatus = authHeader != null ? "OK" : "NULL";
            var tenantStatus = tenantHeader ?? "NULL";
            var usuarioStatus = usuarioHeader ?? "NULL";
            System.Diagnostics.Debug.WriteLine($"🔍 GetAsync - Headers após SetHeaders: Authorization={authStatus}, X-Tenant-Id={tenantStatus}, X-Usuario-Id={usuarioStatus}");
            
            var response = await _httpClient.SendAsync(request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                System.Diagnostics.Debug.WriteLine($"❌ GET {endpoint} - Status: {response.StatusCode}, Error: {errorContent}");
                System.Diagnostics.Debug.WriteLine($"❌ GET {endpoint} - Request Headers: Authorization={authStatus}, X-Tenant-Id={tenantStatus}, X-Usuario-Id={usuarioStatus}");
            }
            
            await EnsureSuccessStatusCode(response);
            var content = await response.Content.ReadAsStringAsync(ct);
            return string.IsNullOrEmpty(content) ? default : JsonSerializer.Deserialize<T>(content, _jsonOptions);
        }

        public async Task<T?> PostAsync<T>(string endpoint, object? data = null, CancellationToken ct = default)
        {
            System.Diagnostics.Debug.WriteLine($"🔍 POST {endpoint}");
            var json = data != null ? JsonSerializer.Serialize(data, _jsonOptions) : null;
            var content = json != null ? new StringContent(json, Encoding.UTF8, "application/json") : null;
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
            SetHeaders(request);
            var response = await _httpClient.SendAsync(request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                System.Diagnostics.Debug.WriteLine($"❌ POST {endpoint} - Status: {response.StatusCode}, Error: {errorContent}");
            }
            
            await EnsureSuccessStatusCode(response);
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            return string.IsNullOrEmpty(responseContent) ? default : JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
        }

        public async Task<T?> PutAsync<T>(string endpoint, object? data = null, CancellationToken ct = default)
        {
            System.Diagnostics.Debug.WriteLine($"🔍 PUT {endpoint}");
            var json = data != null ? JsonSerializer.Serialize(data, _jsonOptions) : null;
            var content = json != null ? new StringContent(json, Encoding.UTF8, "application/json") : null;
            var request = new HttpRequestMessage(HttpMethod.Put, endpoint) { Content = content };
            SetHeaders(request);
            var response = await _httpClient.SendAsync(request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                System.Diagnostics.Debug.WriteLine($"❌ PUT {endpoint} - Status: {response.StatusCode}, Error: {errorContent}");
            }
            
            await EnsureSuccessStatusCode(response);
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            return string.IsNullOrEmpty(responseContent) ? default : JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
        }

        public async Task<bool> DeleteAsync(string endpoint, CancellationToken ct = default)
        {
            System.Diagnostics.Debug.WriteLine($"🔍 DELETE {endpoint}");
            var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
            SetHeaders(request);
            var response = await _httpClient.SendAsync(request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                System.Diagnostics.Debug.WriteLine($"❌ DELETE {endpoint} - Status: {response.StatusCode}, Error: {errorContent}");
            }
            
            await EnsureSuccessStatusCode(response);
            return response.IsSuccessStatusCode;
        }

        public async Task<T?> PatchAsync<T>(string endpoint, object? data = null, CancellationToken ct = default)
        {
            System.Diagnostics.Debug.WriteLine($"🔍 PATCH {endpoint}");
            var json = data != null ? JsonSerializer.Serialize(data, _jsonOptions) : null;
            var content = json != null ? new StringContent(json, Encoding.UTF8, "application/json") : null;
            var request = new HttpRequestMessage(HttpMethod.Patch, endpoint) { Content = content };
            SetHeaders(request);
            var response = await _httpClient.SendAsync(request, ct);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                System.Diagnostics.Debug.WriteLine($"❌ PATCH {endpoint} - Status: {response.StatusCode}, Error: {errorContent}");
            }
            
            await EnsureSuccessStatusCode(response);
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            return string.IsNullOrEmpty(responseContent) ? default : JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
        }

        private async Task EnsureSuccessStatusCode(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var errorMessage = $"Erro HTTP {response.StatusCode}: {errorContent}";
                System.Diagnostics.Debug.WriteLine($"❌ {errorMessage}");
                throw new HttpRequestException(errorMessage);
            }
        }

        public HttpClient GetHttpClient()
        {
            return _httpClient;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
