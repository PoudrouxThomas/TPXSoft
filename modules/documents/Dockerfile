# Build context: repo root (needs Directory.Build.props).
# docker compose build documents-api  /  docker build -f modules/documents/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY modules/documents/src/TPXSoft.Documents.Api/TPXSoft.Documents.Api.csproj modules/documents/src/TPXSoft.Documents.Api/
COPY modules/documents/src/TPXSoft.Documents.Domain/TPXSoft.Documents.Domain.csproj modules/documents/src/TPXSoft.Documents.Domain/
COPY modules/documents/src/TPXSoft.Documents.Infrastructure/TPXSoft.Documents.Infrastructure.csproj modules/documents/src/TPXSoft.Documents.Infrastructure/
RUN dotnet restore modules/documents/src/TPXSoft.Documents.Api/TPXSoft.Documents.Api.csproj

COPY modules/documents/src/ modules/documents/src/
RUN dotnet publish modules/documents/src/TPXSoft.Documents.Api/TPXSoft.Documents.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TPXSoft.Documents.Api.dll"]
