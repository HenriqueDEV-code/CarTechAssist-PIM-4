# STAGE 1 – build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia apenas os .csproj primeiro (build mais rápido e ajuda o restore)
COPY ["CarTechAssist.Api/CarTechAssist.Api.csproj", "CarTechAssist.Api/"]
COPY ["CarTechAssist.Application/CarTechAssist.Application.csproj", "CarTechAssist.Application/"]
COPY ["CarTechAssist.Domain/CarTechAssist.Domain.csproj", "CarTechAssist.Domain/"]
COPY ["CarTechAssist.Infrastruture/CarTechAssist.Infrastruture.csproj", "CarTechAssist.Infrastruture/"]

# Restaura as dependências da API (ele puxa as libs referenciadas)
RUN dotnet restore "CarTechAssist.Api/CarTechAssist.Api.csproj"

# Agora copia o restante do código
COPY . .

# Entra na pasta da API
WORKDIR "/src/CarTechAssist.Api"

# Publica em Release
RUN dotnet publish "CarTechAssist.Api.csproj" -c Release -o /app/publish

# STAGE 2 – runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copia os artefatos publicados
COPY --from=build /app/publish ./

# Porta definida pelo Render
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

ENTRYPOINT ["dotnet", "CarTechAssist.Api.dll"]
