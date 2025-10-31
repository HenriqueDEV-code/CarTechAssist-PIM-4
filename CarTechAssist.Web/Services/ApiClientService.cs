using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CarTechAssist.Web.Services
{
    public class ApiClientService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly JsonSerializerOptions _jsonOptions;

        public ApiClientService(
            HttpClient httpClient,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            
            var baseUrl = _configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5167";
            _httpClient.BaseAddress = new Uri(baseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(
                int.Parse(_configuration["ApiSettings:Timeout"] ?? "30"));
            
            // Configurar headers padrão
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        private void SetHeaders()
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session == null) return;

            var token = session.GetString("Token");
            var tenantId = session.GetString("TenantId");
            var usuarioId = session.GetString("UsuarioId");

            _httpClient.DefaultRequestHeaders.Authorization = null;
            _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");
            _httpClient.DefaultRequestHeaders.Remove("X-Usuario-Id");

            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", token);
            }

            if (!string.IsNullOrEmpty(tenantId))
            {
                _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
            }

            if (!string.IsNullOrEmpty(usuarioId))
            {
                _httpClient.DefaultRequestHeaders.Add("X-Usuario-Id", usuarioId);
            }
        }

        public async Task<T?> GetAsync<T>(string endpoint, CancellationToken ct = default)
        {
            SetHeaders();
            var response = await _httpClient.GetAsync(endpoint, ct);
            await EnsureSuccessStatusCode(response);
            var content = await response.Content.ReadAsStringAsync(ct);
            return string.IsNullOrEmpty(content) ? default : JsonSerializer.Deserialize<T>(content, _jsonOptions);
        }

        public async Task<T?> PostAsync<T>(string endpoint, object? data = null, CancellationToken ct = default)
        {
            SetHeaders();
            var json = data != null ? JsonSerializer.Serialize(data, _jsonOptions) : null;
            var content = json != null ? new StringContent(json, Encoding.UTF8, "application/json") : null;
            
            var response = await _httpClient.PostAsync(endpoint, content, ct);
            await EnsureSuccessStatusCode(response);
            
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            return string.IsNullOrEmpty(responseContent) ? default : JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
        }

        public async Task<T?> PutAsync<T>(string endpoint, object? data = null, CancellationToken ct = default)
        {
            SetHeaders();
            var json = data != null ? JsonSerializer.Serialize(data, _jsonOptions) : null;
            var content = json != null ? new StringContent(json, Encoding.UTF8, "application/json") : null;
            
            var response = await _httpClient.PutAsync(endpoint, content, ct);
            await EnsureSuccessStatusCode(response);
            
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            return string.IsNullOrEmpty(responseContent) ? default : JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
        }

        public async Task<T?> PatchAsync<T>(string endpoint, object? data = null, CancellationToken ct = default)
        {
            SetHeaders();
            var json = data != null ? JsonSerializer.Serialize(data, _jsonOptions) : null;
            var content = json != null ? new StringContent(json, Encoding.UTF8, "application/json") : null;
            
            var request = new HttpRequestMessage(HttpMethod.Patch, endpoint) { Content = content };
            var response = await _httpClient.SendAsync(request, ct);
            await EnsureSuccessStatusCode(response);
            
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            return string.IsNullOrEmpty(responseContent) ? default : JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
        }

        public async Task<bool> DeleteAsync(string endpoint, object? data = null, CancellationToken ct = default)
        {
            SetHeaders();
            
            HttpRequestMessage request;
            if (data != null)
            {
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                request = new HttpRequestMessage(HttpMethod.Delete, endpoint) { Content = content };
            }
            else
            {
                request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
            }
            
            var response = await _httpClient.SendAsync(request, ct);
            await EnsureSuccessStatusCode(response);
            return response.IsSuccessStatusCode;
        }

        public async Task<Stream> GetStreamAsync(string endpoint, CancellationToken ct = default)
        {
            SetHeaders();
            var response = await _httpClient.GetAsync(endpoint, ct);
            await EnsureSuccessStatusCode(response);
            return await response.Content.ReadAsStreamAsync(ct);
        }

        public async Task<T?> PostMultipartAsync<T>(string endpoint, MultipartFormDataContent content, CancellationToken ct = default)
        {
            SetHeaders();
            var response = await _httpClient.PostAsync(endpoint, content, ct);
            await EnsureSuccessStatusCode(response);
            
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            return string.IsNullOrEmpty(responseContent) ? default : JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
        }

        private async Task EnsureSuccessStatusCode(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var errorMessage = $"API retornou status {(int)response.StatusCode}: {response.ReasonPhrase}";
                
                if (!string.IsNullOrEmpty(errorContent))
                {
                    try
                    {
                        var errorObj = JsonSerializer.Deserialize<JsonElement>(errorContent);
                        if (errorObj.TryGetProperty("error", out var errorProp))
                            errorMessage = errorProp.GetString() ?? errorMessage;
                    }
                    catch
                    {
                        // Se não conseguir deserializar, usa o conteúdo bruto
                    }
                }
                
                throw new HttpRequestException(errorMessage, null, response.StatusCode);
            }
        }
    }
}

