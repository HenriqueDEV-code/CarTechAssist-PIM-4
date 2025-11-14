FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia tudo da pasta CarTechAssist.Api (contexto atual) para /src
COPY . .

# Aqui já estamos DENTRO da pasta da API (não existe subpasta CarTechAssist.Api)
# Então o csproj está em /src/CarTechAssist.Api.csproj
RUN dotnet restore "CarTechAssist.Api.csproj"

RUN dotnet publish "CarTechAssist.Api.csproj" -c Release -o /app/publish

# ---------- RUNTIME ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

ENTRYPOINT ["dotnet", "CarTechAssist.Api.dll"]
