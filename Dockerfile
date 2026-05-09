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

# 5. Copy the rest of the source code
COPY . .

# 6. FIX: Target the correct path for the publish command
FROM build AS publish-hrms
RUN dotnet publish "hrms-api/hrms-api.csproj" -c Release -o /app/publish/hrms

FROM build AS publish-adms
RUN dotnet publish "hrms-adms-api/hrms-adms-api.csproj" -c Release -o /app/publish/adms

# Final Stage

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS hrms-service
WORKDIR /app
COPY --from=publish-hrms /app/publish/hrms .
ENTRYPOINT ["dotnet", "hrms-api.dll"]


FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS adms-service
WORKDIR /app
COPY --from=publish-adms /app/publish/adms .
ENTRYPOINT ["dotnet", "hrms-adms-api.dll"]
