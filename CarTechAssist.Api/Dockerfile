FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia TODO o código da solução
COPY . .

# Entra na pasta da API
WORKDIR /src/CarTechAssist.Api

# Restaura os pacotes da solução a partir da API
RUN dotnet restore

# Publica em Release
RUN dotnet publish -c Release -o /app/publish

# ---------- RUNTIME ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

ENTRYPOINT ["dotnet", "CarTechAssist.Api.dll"]
