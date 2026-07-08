FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# 1. Copy the Central Package Management file first for caching
COPY ["Directory.Packages.props", "./"]

# 2. Copy .csproj files - ensuring paths match your solution structure 
COPY ["hrms-api/hrms-api.csproj", "hrms-api/"]
COPY ["Hrms.Core/Hrms.Core.csproj", "Hrms.Core/"]
COPY ["hrms.Domain/hrms.Domain.csproj", "hrms.Domain/"]
COPY ["hrms.Infrastructure/hrms.Infrastructure.csproj", "hrms.Infrastructure/"]
COPY ["dtr-api/DTR.Core/DTR.Core.csproj", "dtr-api/DTR.Core/"]
COPY ["MigrationHrms/MigrationHrms.csproj", "MigrationHrms/"]

# 3. Copy submodule .csproj files
COPY ["tenantstore/Services/Shared/Onepunch.Common.Lib/Onepunch.Common.Lib.csproj", "tenantstore/Services/Shared/Onepunch.Common.Lib/"]
COPY ["tenantstore/Services/TenantApi/TenantStoreApi.Infrastructure/TenantStoreApi.Infrastructure.csproj", "tenantstore/Services/TenantApi/TenantStoreApi.Infrastructure/"]
COPY ["tenantstore/Services/TenantApi/TenantStoreApi.Domain/TenantStoreApi.Domain.csproj", "tenantstore/Services/TenantApi/TenantStoreApi.Domain/"]

COPY ["hrms-adms-api/hrms-adms-api.csproj", "hrms-adms-api/"]

# 4. Restore dependencies
RUN dotnet restore "hrms-api/hrms-api.csproj"
RUN dotnet restore "hrms-adms-api/hrms-adms-api.csproj"
RUN dotnet restore "MigrationHrms/MigrationHrms.csproj"

# 5. Copy the rest of the source code
COPY . .

# 6. Publish compilation stages (Bypassing escalated warnings to avoid workflow crashes)
FROM build AS publish-hrms
RUN dotnet publish "hrms-api/hrms-api.csproj" -c Release -o /app/publish/hrms /p:TreatWarningsAsErrors=false

FROM build AS publish-adms
RUN dotnet publish "hrms-adms-api/hrms-adms-api.csproj" -c Release -o /app/publish/adms /p:TreatWarningsAsErrors=false

FROM build AS publish-migration
RUN dotnet publish "MigrationHrms/MigrationHrms.csproj" -c Release -o /app/publish/migration

# ==========================================
# Final Runtime Target Stages
# ==========================================

# HRMS Core API Service
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS hrms-service
WORKDIR /app
COPY --from=publish-hrms /app/publish/hrms .
ENTRYPOINT ["dotnet", "hrms-api.dll"]

# Biometric ADMS API Service
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS adms-service
WORKDIR /app
COPY --from=publish-adms /app/publish/adms .
ENTRYPOINT ["dotnet", "hrms-adms-api.dll"]

# Migration Worker Console App
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS migration-service
WORKDIR /app
COPY --from=publish-migration /app/publish/migration .
ENTRYPOINT ["dotnet", "MigrationHrms.dll"]