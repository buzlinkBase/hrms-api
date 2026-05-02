FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Note the 'hrms-api/' prefix because we are building from the VsCode folder
COPY ["hrms-api/hrms-api/hrms-api.csproj", "hrms-api/hrms-api/"]
COPY ["hrms-api/Hrms.Core/Hrms.Core.csproj", "hrms-api/Hrms.Core/"]
COPY ["hrms-api/hrms.Domain/hrms.Domain.csproj", "hrms-api/hrms.Domain/"]
COPY ["hrms-api/hrms.Infrastructure/hrms.Infrastructure.csproj", "hrms-api/hrms.Infrastructure/"]
COPY ["hrms-api/dtr-api/DTR.Core/DTR.Core.csproj", "hrms-api/dtr-api/DTR.Core/"]

# Copy the common library from the sibling folder
COPY ["tenantstore/TenantStore/Onepunch.Common.Lib/Onepunch.Common.Lib.csproj", "tenantstore/TenantStore/Onepunch.Common.Lib/"]

RUN dotnet restore "hrms-api/hrms-api/hrms-api.csproj"

COPY . .
WORKDIR "/src/hrms-api/hrms-api"
RUN dotnet publish "hrms-api.csproj" -c Release -o /app/publish

# Final Stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Hrms.Api.dll"]