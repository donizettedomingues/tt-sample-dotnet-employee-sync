# syntax=docker/dockerfile:1

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached until a project file changes).
COPY src/EmployeeSync.Domain/EmployeeSync.Domain.csproj src/EmployeeSync.Domain/
COPY src/EmployeeSync.Application/EmployeeSync.Application.csproj src/EmployeeSync.Application/
COPY src/EmployeeSync.Infrastructure/EmployeeSync.Infrastructure.csproj src/EmployeeSync.Infrastructure/
COPY src/EmployeeSync.Api/EmployeeSync.Api.csproj src/EmployeeSync.Api/
RUN dotnet restore src/EmployeeSync.Api/EmployeeSync.Api.csproj

COPY src/ src/
RUN dotnet publish src/EmployeeSync.Api/EmployeeSync.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Non-root user provided by the official .NET images.
USER $APP_UID
ENTRYPOINT ["dotnet", "EmployeeSync.Api.dll"]
