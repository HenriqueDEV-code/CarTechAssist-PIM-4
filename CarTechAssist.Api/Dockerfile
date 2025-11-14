# STAGE 1 – build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["CarTechAssist.Api/CarTechAssist.Api.csproj", "CarTechAssist.Api/"]
COPY ["CarTechAssist.Application/CarTechAssist.Application.csproj", "CarTechAssist.Application/"]
COPY ["CarTechAssist.Contracts/CarTechAssist.Contracts.csproj", "CarTechAssist.Contracts/"]
COPY ["CarTechAssist.Infrastruture/CarTechAssist.Infrastruture.csproj", "CarTechAssist.Infrastruture/"]

RUN dotnet restore "CarTechAssist.Api/CarTechAssist.Api.csproj"

COPY . .

WORKDIR "/src/CarTechAssist.Api"
RUN dotnet publish -c Release -o /app/publish

# STAGE 2 – runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}

ENTRYPOINT ["dotnet", "CarTechAssist.Api.dll"]
