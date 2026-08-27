# Build context: repo root (needs Directory.Build.props).
# docker compose build auth-api  /  docker build -f modules/auth/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY modules/auth/src/TPXSoft.Auth.Api/TPXSoft.Auth.Api.csproj modules/auth/src/TPXSoft.Auth.Api/
COPY modules/auth/src/TPXSoft.Auth.Domain/TPXSoft.Auth.Domain.csproj modules/auth/src/TPXSoft.Auth.Domain/
COPY modules/auth/src/TPXSoft.Auth.Infrastructure/TPXSoft.Auth.Infrastructure.csproj modules/auth/src/TPXSoft.Auth.Infrastructure/
RUN dotnet restore modules/auth/src/TPXSoft.Auth.Api/TPXSoft.Auth.Api.csproj

COPY modules/auth/src/ modules/auth/src/
RUN dotnet publish modules/auth/src/TPXSoft.Auth.Api/TPXSoft.Auth.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TPXSoft.Auth.Api.dll"]
