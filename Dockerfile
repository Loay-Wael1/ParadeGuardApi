FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER root
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ParadeGuard.Api.csproj", "."]
RUN dotnet restore "ParadeGuard.Api.csproj"
COPY . .
RUN dotnet build "ParadeGuard.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ParadeGuard.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ParadeGuard.Api.dll"]