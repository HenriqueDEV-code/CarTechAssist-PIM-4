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

            // CORREÇÃO: A API retorna PascalCase (padrão .NET), não CamelCase
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
                // Não usar CamelCase pois a API retorna PascalCase
                // PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        // CORREÇÃO: Usar headers por requisição em vez de DefaultRequestHeaders (problema de concorrência)
        private void SetHeaders(HttpRequestMessage? request = null)
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session == null) return;

            var token = session.GetString("Token");
            var tenantId = session.GetString("TenantId");
            var usuarioId = session.GetString("UsuarioId");

            // CORREÇÃO: Se for requisição específica, adicionar headers nela
            // Se não, limpar e adicionar nos DefaultRequestHeaders (para compatibilidade)
            if (request != null)
            {
                // Adicionar headers na requisição específica
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                if (!string.IsNullOrEmpty(tenantId))
                {
                    request.Headers.Add("X-Tenant-Id", tenantId);
                }

                if (!string.IsNullOrEmpty(usuarioId))
                {
                    request.Headers.Add("X-Usuario-Id", usuarioId);
                }
            }
            else
            {
                // Limpar headers padrão (para métodos que usam DefaultRequestHeaders)
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
            // CORREÇÃO: Usar HttpRequestMessage para adicionar headers por requisição
            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            SetHeaders(request);
            var response = await _httpClient.SendAsync(request, ct);
            await EnsureSuccessStatusCode(response);
            var content = await response.Content.ReadAsStringAsync(ct);
            return string.IsNullOrEmpty(content) ? default : JsonSerializer.Deserialize<T>(content, _jsonOptions);
        }

        public async Task<T?> PostAsync<T>(string endpoint, object? data = null, CancellationToken ct = default)
        {
            // CORREÇÃO: Usar HttpRequestMessage para adicionar headers por requisição
            var json = data != null ? JsonSerializer.Serialize(data, _jsonOptions) : null;
            var content = json != null ? new StringContent(json, Encoding.UTF8, "application/json") : null;
            
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
            SetHeaders(request);
            var response = await _httpClient.SendAsync(request, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            
            // Se for 400 (BadRequest), tentar deserializar a resposta mesmo sendo erro
            // Isso permite que o código leia mensagens de erro estruturadas (ex: { success: false, message: "..." })
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                if (!string.IsNullOrEmpty(responseContent))
                {
                    try
                    {
                        var errorObj = JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
                        // Se conseguiu deserializar, retornar objeto (o controller vai verificar success: false)
                        // Isso permite que mensagens de erro estruturadas sejam lidas
                        if (errorObj != null)
                            return errorObj;
                    }
                    catch
                    {
                        // Se não conseguir deserializar, continua e lança exceção
                    }
                }
                // Se chegou aqui, não conseguiu deserializar - lança exceção com a mensagem
                await EnsureSuccessStatusCode(response);
                return default; // Nunca chega aqui, mas compilador precisa
            }
            
            // Para outros status codes, verificar sucesso normalmente
            if (!response.IsSuccessStatusCode)
            {
                await EnsureSuccessStatusCode(response);
            }
            
            return string.IsNullOrEmpty(responseContent) ? default : JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
        }

        public async Task<T?> PutAsync<T>(string endpoint, object? data = null, CancellationToken ct = default)
        {
            // CORREÇÃO: Usar HttpRequestMessage para adicionar headers por requisição
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
            // CORREÇÃO: Headers já são adicionados no request abaixo
            var json = data != null ? JsonSerializer.Serialize(data, _jsonOptions) : null;
            var content = json != null ? new StringContent(json, Encoding.UTF8, "application/json") : null;
            
            var request = new HttpRequestMessage(HttpMethod.Patch, endpoint) { Content = content };
            SetHeaders(request);
            var response = await _httpClient.SendAsync(request, ct);
            await EnsureSuccessStatusCode(response);
            
            var responseContent = await response.Content.ReadAsStringAsync(ct);
            return string.IsNullOrEmpty(responseContent) ? default : JsonSerializer.Deserialize<T>(responseContent, _jsonOptions);
        }

        public async Task<bool> DeleteAsync(string endpoint, object? data = null, CancellationToken ct = default)
        {
            // CORREÇÃO: Headers já são adicionados no request abaixo
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
            // CORREÇÃO: Usar HttpRequestMessage para adicionar headers por requisição
            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            SetHeaders(request);
            var response = await _httpClient.SendAsync(request, ct);
            await EnsureSuccessStatusCode(response);
            return await response.Content.ReadAsStreamAsync(ct);
        }

        public async Task<T?> PostMultipartAsync<T>(string endpoint, MultipartFormDataContent content, CancellationToken ct = default)
        {
            // CORREÇÃO: Usar HttpRequestMessage para adicionar headers por requisição
            var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = content };
            SetHeaders(request);
            var response = await _httpClient.SendAsync(request, ct);
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
                string? messageFromApi = null;
                
                if (!string.IsNullOrEmpty(errorContent))
                {
                    try
                    {
                        var errorObj = JsonSerializer.Deserialize<JsonElement>(errorContent);
                        
                        // Tentar ler a mensagem de erro da API
                        if (errorObj.TryGetProperty("message", out var messageProp))
                        {
                            messageFromApi = messageProp.GetString();
                        }
                        else if (errorObj.TryGetProperty("error", out var errorProp))
                        {
                            messageFromApi = errorProp.GetString();
                        }
                        
                        // Se encontrou mensagem da API, usar ela
                        if (!string.IsNullOrEmpty(messageFromApi))
                        {
                            errorMessage = messageFromApi;
                        }
                    }
                    catch
                    {
                        // Se não conseguir deserializar, usa o conteúdo bruto
                        if (!string.IsNullOrEmpty(errorContent))
                        {
                            errorMessage = errorContent;
                        }
                    }
                }
                
                // Armazenar a mensagem de erro e o conteúdo completo no Data da exceção
                var exception = new HttpRequestException(errorMessage, null, response.StatusCode);
                exception.Data["StatusCode"] = response.StatusCode;
                exception.Data["ResponseContent"] = errorContent;
                exception.Data["Message"] = messageFromApi ?? errorMessage;
                
                throw exception;
            }
        }
    }
}

