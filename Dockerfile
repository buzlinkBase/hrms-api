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
COPY ["tenantstore/TenantStore/Onepunch.Common.Lib/Onepunch.Common.Lib.csproj", "tenantstore/TenantStore/Onepunch.Common.Lib/"]
COPY ["tenantstore/TenantStore/TenantStoreApi.Infrastructure/TenantStoreApi.Infrastructure.csproj", "tenantstore/TenantStore/TenantStoreApi.Infrastructure/"]
COPY ["tenantstore/TenantStore/TenantStoreApi.Domain/TenantStoreApi.Domain.csproj", "tenantstore/TenantStore/TenantStoreApi.Domain/"]

# 4. Restore dependencies
RUN dotnet restore "hrms-api/hrms-api.csproj"

# 5. Copy the rest of the source code
COPY . .

# 6. FIX: Target the correct path for the publish command
RUN dotnet publish "hrms-api/hrms-api.csproj" -c Release -o /app/publish

# Final Stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# 7. Ensure the DLL name matches your project output (case-sensitive on Linux)
ENTRYPOINT ["dotnet", "hrms-api.dll"]