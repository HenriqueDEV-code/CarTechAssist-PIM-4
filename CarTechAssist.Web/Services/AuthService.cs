using CarTechAssist.Contracts.Auth;

namespace CarTechAssist.Web.Services
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
            return await _apiClient.PostAsync<LoginResponse>("api/auth/login", request, ct);
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

