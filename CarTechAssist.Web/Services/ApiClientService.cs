using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarTechAssist.Web.Services
{
    public class ApiClientService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ILogger<ApiClientService>? _logger;

        public ApiClientService(
            HttpClient httpClient,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            ILogger<ApiClientService>? logger = null)
        {
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _logger = logger;
            
            var baseUrl = _configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5167";
            _httpClient.BaseAddress = new Uri(baseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(
                int.Parse(_configuration["ApiSettings:Timeout"] ?? "30"));

            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");


            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,

                PropertyNamingPolicy = null // null = PascalCase (padrão .NET)
            };
        }

        private void SetHeaders(HttpRequestMessage? request = null)
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session == null)
            {
                _logger?.LogWarning("⚠️ SetHeaders - Session é NULL!");
                return;
            }

            var token = session.GetString("Token");
            var tenantId = session.GetString("TenantId");
            var usuarioId = session.GetString("UsuarioId");

            _logger?.LogInformation("🔍 SetHeaders - Token: {TokenStatus}, TenantId: {TenantId}, UsuarioId: {UsuarioId}", 
                string.IsNullOrEmpty(token) ? "NULL" : "OK", tenantId, usuarioId);


            if (request != null)
            {

                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    _logger?.LogInformation("✅ SetHeaders - Authorization header adicionado");
                }
                else
                {
                    _logger?.LogWarning("❌ SetHeaders - Token não encontrado na sessão!");
                }

                if (!string.IsNullOrEmpty(tenantId))
                {
                    request.Headers.Add("X-Tenant-Id", tenantId);
                    _logger?.LogInformation("✅ SetHeaders - X-Tenant-Id: {TenantId}", tenantId);
                }

                if (!string.IsNullOrEmpty(usuarioId))
                {
                    request.Headers.Add("X-Usuario-Id", usuarioId);
                    _logger?.LogInformation("✅ SetHeaders - X-Usuario-Id: {UsuarioId}", usuarioId);
                }
            }
            else
            {

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
        }

        public async Task<T?> GetAsync<T>(string endpoint, CancellationToken ct = default)
        {

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            SetHeaders(request);
            var response = await _httpClient.SendAsync(request, ct);
            await EnsureSuccessStatusCode(response);
            var content = await response.Content.ReadAsStringAsync(ct);
            return string.IsNullOrEmpty(content) ? default : JsonSerializer.Deserialize<T>(content, _jsonOptions);
        }



        public async Task<T?> PostAsyncSemAuth<T>(string endpoint, object? data = null, CancellationToken ct = default)
        {
            var json = data != null ? JsonSerializer.Serialize(data, _jsonOptions) : null;
            var content = json != null ? new StringContent(json, Encoding.UTF8, "application/json") : null;
            
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };

            var response = await _httpClient.SendAsync(request, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                await EnsureSuccessStatusCode(response);
                return default; // Nunca chega aqui, mas compilador precisa
            }

            if (string.IsNullOrEmpty(responseContent))
            {
                return default;
            }
            
            return JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
        }

        public async Task<T?> PostAsync<T>(string endpoint, object? data = null, CancellationToken ct = default)
        {

            var json = data != null ? JsonSerializer.Serialize(data, _jsonOptions) : null;
            var content = json != null ? new StringContent(json, Encoding.UTF8, "application/json") : null;
            
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
            SetHeaders(request);

            if (data != null)
            {
                _logger?.LogInformation("🔍 POST {Endpoint} - JSON enviado: {Json}", endpoint, json);
                _logger?.LogInformation("🔍 POST {Endpoint} - Headers: Authorization={HasAuth}, X-Tenant-Id={HasTenantId}", 
                    endpoint, request.Headers.Authorization != null, request.Headers.Contains("X-Tenant-Id"));
            }
            
            var response = await _httpClient.SendAsync(request, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            var responsePreview = responseContent?.Length > 500 ? responseContent.Substring(0, 500) + "..." : responseContent;
            _logger?.LogInformation("🔍 POST {Endpoint} - Status: {StatusCode}, Response: {Response}", 
                endpoint, response.StatusCode, responsePreview);

            if (!response.IsSuccessStatusCode)
            {
                await EnsureSuccessStatusCode(response, responseContent);
                return default; // Nunca chega aqui, mas compilador precisa
            }

            return string.IsNullOrEmpty(responseContent) ? default : JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
        }

        public async Task<T?> PutAsync<T>(string endpoint, object? data = null, CancellationToken ct = default)
        {

            var json = data != null ? JsonSerializer.Serialize(data, _jsonOptions) : null;
            var content = json != null ? new StringContent(json, Encoding.UTF8, "application/json") : null;
            
            var request = new HttpRequestMessage(HttpMethod.Put, endpoint) { Content = content };
            SetHeaders(request);
            var response = await _httpClient.SendAsync(request, ct);
            await EnsureSuccessStatusCode(response);
            
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            return string.IsNullOrEmpty(responseContent) ? default : JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
        }

        public async Task<T?> PatchAsync<T>(string endpoint, object? data = null, CancellationToken ct = default)
        {
            var json = data != null ? JsonSerializer.Serialize(data, _jsonOptions) : null;
            var content = json != null ? new StringContent(json, Encoding.UTF8, "application/json") : null;
            
            var request = new HttpRequestMessage(HttpMethod.Patch, endpoint) { Content = content };
            SetHeaders(request);
            
            if (data != null)
            {
                _logger?.LogInformation("🔍 PATCH {Endpoint} - JSON enviado: {Json}", endpoint, json);
                _logger?.LogInformation("🔍 PATCH {Endpoint} - Headers: Authorization={HasAuth}, X-Tenant-Id={HasTenantId}", 
                    endpoint, request.Headers.Authorization != null, request.Headers.Contains("X-Tenant-Id"));
            }
            
            var response = await _httpClient.SendAsync(request, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            
            var responsePreview = responseContent?.Length > 500 ? responseContent.Substring(0, 500) + "..." : responseContent;
            _logger?.LogInformation("🔍 PATCH {Endpoint} - Status: {StatusCode}, Response: {Response}", 
                endpoint, response.StatusCode, responsePreview);
            
            if (!response.IsSuccessStatusCode)
            {
                await EnsureSuccessStatusCode(response, responseContent);
                return default; // Nunca chega aqui, mas compilador precisa
            }
            
            return string.IsNullOrEmpty(responseContent) ? default : JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
        }

        public async Task<bool> DeleteAsync(string endpoint, object? data = null, CancellationToken ct = default)
        {

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
            
            SetHeaders(request);
            var response = await _httpClient.SendAsync(request, ct);
            await EnsureSuccessStatusCode(response);
            return response.IsSuccessStatusCode;
        }

        public async Task<Stream> GetStreamAsync(string endpoint, CancellationToken ct = default)
        {

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            SetHeaders(request);
            var response = await _httpClient.SendAsync(request, ct);
            await EnsureSuccessStatusCode(response);
            return await response.Content.ReadAsStreamAsync(ct);
        }

        public async Task<T?> PostMultipartAsync<T>(string endpoint, MultipartFormDataContent content, CancellationToken ct = default)
        {

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
            SetHeaders(request);
            var response = await _httpClient.SendAsync(request, ct);
            await EnsureSuccessStatusCode(response);
            
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            return string.IsNullOrEmpty(responseContent) ? default : JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
        }

        private async Task EnsureSuccessStatusCode(HttpResponseMessage response, string? responseContent = null)
        {
            if (!response.IsSuccessStatusCode)
            {

                var errorContent = responseContent ?? await response.Content.ReadAsStringAsync();
                var errorMessage = $"API retornou status {(int)response.StatusCode}: {response.ReasonPhrase}";
                string? messageFromApi = null;
                
                if (!string.IsNullOrEmpty(errorContent))
                {
                    try
                    {
                        var errorObj = JsonSerializer.Deserialize<JsonElement>(errorContent);

                        if (errorObj.TryGetProperty("message", out var messageProp))
                        {
                            messageFromApi = messageProp.GetString();
                        }
                        else if (errorObj.TryGetProperty("error", out var errorProp))
                        {
                            messageFromApi = errorProp.GetString();
                        }
                        else if (errorObj.TryGetProperty("title", out var titleProp))
                        {

                            messageFromApi = titleProp.GetString();
                            if (errorObj.TryGetProperty("errors", out var errorsProp))
                            {

                                var errorsDetail = errorsProp.ToString();
                                if (!string.IsNullOrEmpty(errorsDetail))
                                {
                                    messageFromApi += " " + errorsDetail;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(messageFromApi))
                        {
                            errorMessage = messageFromApi;
                        }
                    }
                    catch
                    {

                        if (!string.IsNullOrEmpty(errorContent))
                        {
                            errorMessage = errorContent;
                        }
                    }
                }

                var exception = new HttpRequestException(errorMessage, null, response.StatusCode);
                exception.Data["StatusCode"] = response.StatusCode;
                exception.Data["ResponseContent"] = errorContent;
                exception.Data["Message"] = messageFromApi ?? errorMessage;
                
                throw exception;
            }
        }
    }
}

