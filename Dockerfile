# ---------- BUILD ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia o repositório inteiro
COPY . .

# Restaura a partir do projeto da API (que referencia os outros)
RUN dotnet restore "CarTechAssist.Api/CarTechAssist.Api.csproj"

# Publica a API em Release
RUN dotnet publish "CarTechAssist.Api/CarTechAssist.Api.csproj" -c Release -o /app/publish

# ---------- RUNTIME ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

# Porta que o Render injeta
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

ENTRYPOINT ["dotnet", "CarTechAssist.Api.dll"]
