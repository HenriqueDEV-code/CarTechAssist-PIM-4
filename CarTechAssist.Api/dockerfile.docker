
# STAGE 1 – build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia o repositório inteiro para dentro do container
COPY . .

# Entra na pasta da API
WORKDIR /src/CarTechAssist.Api

# Restaura e publica
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

# STAGE 2 – runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copia o resultado da publicação
COPY --from=build /app/publish ./

# Render injeta a porta em $PORT
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

# Nome da DLL de saída da sua API
ENTRYPOINT ["dotnet", "CarTechAssist.Api.dll"]
