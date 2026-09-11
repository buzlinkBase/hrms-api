using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Hrms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class init_config : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AllowanceTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllowanceTypes", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Code = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ShortName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Address = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Contact = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ManagerName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Boundary = table.Column<Polygon>(type: "geometry", nullable: true)
                        .Annotation("MySql:SpatialReferenceSystemId", 4326),
                    RegionCode = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WageOrderClass = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ClientBillingInfos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ClientId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Tin = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BillingAddress = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BillingContactName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BillingEmail = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BillingPhone = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PaymentTermsDays = table.Column<int>(type: "int", nullable: false),
                    BillingCycle = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Currency = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientBillingInfos", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ClientRateTables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ClientId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Type = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientRateTables", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Code = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Phone = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Address = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContactPerson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Code = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ShortName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Address = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Contact = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TotalWorkingDays = table.Column<int>(type: "int", nullable: false),
                    TakehomePercentage = table.Column<int>(type: "int", nullable: false),
                    ApplyStatutoryOnActualMonth = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TIN = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RDOCode = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SSSNumber = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PhilHealthNumber = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PagIbigNumber = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AuthorizedSignatoryName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AuthorizedSignatoryTitle = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DeductionApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DeductionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EncodeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FrequencyOfPayment = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Terms = table.Column<int>(type: "int", nullable: false),
                    TotalPrincipal = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    InterestRate = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    ProcessBy = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Note = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Remarks = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApprovalStatus = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeductionApplications", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DeductionPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DeductionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayrollDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeductionPayments", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DeductionTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Code = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeductionTypes", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GeneralSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IdentityType = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IdentityTypeId = table.Column<string>(type: "varchar(255)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Value = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Metadata = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneralSettings", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GovAnnualTaxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RangeFrom = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RangeTo = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    BaseTaxDue = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    AddOnPercentage = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GovAnnualTaxes", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GovHDMFs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MinSalaryBase = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    MaxSalaryBase = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    EmployeeRate = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    EmployerRate = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    EmployeeShare = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    EmployerShare = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TotalContribution = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Remarks = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GovHDMFs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GovPHICs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MinSalaryBase = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    MaxSalaryBase = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    PremiumRate = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    EmployeeShare = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    EmployerShare = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TotalContribution = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Remarks = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GovPHICs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GovSSSes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RangeFrom = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RangeTo = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    MSC = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    EE = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    ER = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    EC = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TotalContibution = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GovSSSes", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GovTaxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PayrollType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RangeFrom = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RangeTo = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    PercentageInAmountOf = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    BaseTaxDue = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    AddOnPercentage = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GovTaxes", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "HDMFContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayrollBatchId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayrollFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    PayrollTo = table.Column<DateOnly>(type: "date", nullable: false),
                    PayrollDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EmployeeShare = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    EmployerShare = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TotalContribution = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HDMFContributions", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "InboxState",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    MessageId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ConsumerId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LockId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RowVersion = table.Column<DateTime>(type: "timestamp(6)", rowVersion: true, nullable: true),
                    Received = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReceiveCount = table.Column<int>(type: "int", nullable: false),
                    ExpirationTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Consumed = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Delivered = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxState", x => x.Id);
                    table.UniqueConstraint("AK_InboxState_MessageId_ConsumerId", x => new { x.MessageId, x.ConsumerId });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "IncomePayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IncomeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayrollDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomePayments", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Leaves",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Code = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LegalBasis = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Remarks = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PaySource = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EmployerAdvancesPayment = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AccrualBasis = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Credits = table.Column<double>(type: "double", nullable: false),
                    AccrualRate = table.Column<double>(type: "double", nullable: false),
                    MaxAccrualBalance = table.Column<double>(type: "double", nullable: true),
                    ProRateFirstYear = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LeaveReset = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MinServiceMonths = table.Column<int>(type: "int", nullable: false),
                    GenderRestriction = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequiresApproval = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RequiresSupportingDocument = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AllowHalfDay = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AllowPartial = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AllowNegativeBalance = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RequiresCredits = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MaxDaysPerYear = table.Column<double>(type: "double", nullable: true),
                    MaxConsecutiveDays = table.Column<int>(type: "int", nullable: true),
                    CarryOverType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CarryOverMaxDays = table.Column<double>(type: "double", nullable: false),
                    CarryOverExpiryMonths = table.Column<int>(type: "int", nullable: true),
                    ConvertToCash = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CashConversionRate = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    MaxCashConversionDays = table.Column<double>(type: "double", nullable: true),
                    IsStatutory = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leaves", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ManualAttendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BatchCode = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    User = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Time1 = table.Column<TimeSpan>(type: "time(6)", nullable: true),
                    Time2 = table.Column<TimeSpan>(type: "time(6)", nullable: true),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualAttendance", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MinimumWageRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RegionCode = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RegionName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DailyRate = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WageOrderNo = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WageOrderClass = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinimumWageRates", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OtherIncomeApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EncodeDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FrequencyOfPayment = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IncomeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Amount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    ProcessBy = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsProrated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsTaxable = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Remarks = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtherIncomeApplications", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                columns: table => new
                {
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EnqueueTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    SentTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Headers = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Properties = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InboxMessageId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    InboxConsumerId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    OutboxId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    MessageId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ContentType = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MessageType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Body = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConversationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CorrelationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    InitiatorId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    RequestId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    SourceAddress = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DestinationAddress = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResponseAddress = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FaultAddress = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExpirationTime = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.SequenceNumber);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OutboxState",
                columns: table => new
                {
                    OutboxId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LockId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RowVersion = table.Column<DateTime>(type: "timestamp(6)", rowVersion: true, nullable: true),
                    Created = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Delivered = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxState", x => x.OutboxId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PayrollBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayPeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PayPeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    PayDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DtrBatchCodes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Remarks = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsPosted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PostedBy = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    PayrollType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollBatches", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PayrollGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Code = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PayrollFrequency = table.Column<int>(type: "int", nullable: false),
                    StatutoryDeductionSchedule = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollGroups", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Payrolls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BatchCode = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PayPeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PayPeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    PayrollDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PostingPeriod = table.Column<DateOnly>(type: "date", nullable: false),
                    PayrollBatchId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Remarks = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FullName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PayrollPeriod = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DailyRate = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    SalaryType = table.Column<int>(type: "int", nullable: false),
                    TotalLoans = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    OTPremiumPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    NDPremiumPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    OvertimePay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    NightDifferentialPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    NightDifferentialOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    BasicPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RegularOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RegularNDPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RegularNDOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDayPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDayOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDayNDPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDayNDOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    LegalPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    LegalOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    LegalNDPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    LegalNDOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    SpecialPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    SpecialOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    SpecialNDPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    SpecialNDOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestLegalPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestLegalOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestLegalNDPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestLegalNDOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestSpecialPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestSpecialOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestSpecialNDPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestSpecialNDOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    DoubleLegalPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    DoubleLegalOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    DoubleLegalNDPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    DoubleLegalNDOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDoubleLegalPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDoubleLegalOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDoubleLegalNDPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDoubleLegalNDOTPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    HolidayPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    LegalHolidayUnworkedPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Cola = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TotalRegularAllowances = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TotalBonuses = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Reimbursement = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TotalOtherIncome = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TotalDeminimises = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TotalCommissions = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    GrossIncome = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    WithholdingTax = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    SSSContribution = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    PhilHealthContribution = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    PagIbigContribution = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    OtherDeductions = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    UnpaidLeaves = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    PaidLeaves = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    NonCompanyPaidLeaves = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    GovernmentFundedLeavePay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    CompanyFundedLeavePay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    OneTimePayoutBreakdown = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PaidLeaveBreakdown = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AbsencesAmount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    LateAmount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    UnderTimeAmount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    GrossPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    NetPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    EmployerSSSContribution = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    EmployerPhilHealthContribution = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    EmployerPagIbigContribution = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    EmployerECContribution = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    PayrollGroupId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    AreaId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ClientId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    NonTaxableBenefits = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TaxableBenefits = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TaxableIncome = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    NonTaxableIncome = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    IsPosted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PayrollType = table.Column<int>(type: "int", nullable: false),
                    OvertimeHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RegularNetHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RegularOTHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RegularNDHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RegularNDOTHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDayHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDayOTHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDayNDHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDayNDOTHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    LegalHolHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    LegalHolOTHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    LegalHolNightDiffHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    LegalHolNightDiffOTHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    SpecialHolHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    SpecialHolOTHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    SpecialHolNightDiffHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    SpecialHolNightDiffOTHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestLegalDayHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestLegalDayOTHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestLegalDayNDHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestLegalDayNDOTHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestSpecialDayHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestSpecialDayOTHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestSpecialDayNDHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestSpecialDayNDOTHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    DoubleLegalHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    DoubleLegalOTHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    DoubleLegalNDHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    DoubleLegalNDOTHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDoubleLegalHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDoubleLegalOTHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDoubleLegalNDHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RestDoubleLegalNDOTHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    OBHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    PaidLeaveHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    UnpaidLeaveHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payrolls", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PHICContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayrollBatchId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayrollFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    PayrollTo = table.Column<DateOnly>(type: "date", nullable: false),
                    PayrollDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EmployeeShare = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    EmployerShare = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TotalContribution = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PHICContributions", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Positions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Code = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Rate = table.Column<double>(type: "double", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PremiumRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Type = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ShortDescription = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Remarks = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PremiumRates", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ProratedAllowances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayrollFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    PayrollTo = table.Column<DateOnly>(type: "date", nullable: false),
                    PayrollDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Amount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    IsPosted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProratedAllowances", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SalaryAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AdjustmentType = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayrollDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Remarks = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConsumedByPayrollId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryAdjustments", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SSSContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayrollBatchId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayrollFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    PayrollTo = table.Column<DateOnly>(type: "date", nullable: false),
                    PayrollDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EE = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    ER = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    EC = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TotalContibution = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Remarks = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SSSContributions", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TaxContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayrollBatchId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayrollFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    PayrollTo = table.Column<DateOnly>(type: "date", nullable: false),
                    PayrollDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxContributions", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TimeShifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ShiftName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ShiftType = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time(6)", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time(6)", nullable: false),
                    WithAMBreak = table.Column<int>(type: "int", nullable: false),
                    AMStartTime = table.Column<TimeSpan>(type: "time(6)", nullable: true),
                    AMEndTime = table.Column<TimeSpan>(type: "time(6)", nullable: true),
                    WithLunchBreak = table.Column<int>(type: "int", nullable: false),
                    LunchStartTime = table.Column<TimeSpan>(type: "time(6)", nullable: true),
                    LunchEndTime = table.Column<TimeSpan>(type: "time(6)", nullable: true),
                    WithPMBreak = table.Column<int>(type: "int", nullable: false),
                    PMStartTime = table.Column<TimeSpan>(type: "time(6)", nullable: true),
                    PMEndTime = table.Column<TimeSpan>(type: "time(6)", nullable: true),
                    GracePeriodMinutes = table.Column<double>(type: "double", nullable: false),
                    BreakDurationMinutes = table.Column<double>(type: "double", nullable: false),
                    WithOT = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    OTRequireTimeIn = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    OTStart = table.Column<TimeSpan>(type: "time(6)", nullable: false),
                    OverTimeThreshold = table.Column<double>(type: "double", nullable: false),
                    MaxOvertimeHours = table.Column<double>(type: "double", nullable: true),
                    MinimumWorkMinutes = table.Column<double>(type: "double", nullable: false),
                    MaxWorkingMinutes = table.Column<double>(type: "double", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeShifts", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "YearLocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    IsLocked = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LockedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReopenedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YearLocks", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Allowances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IncomeClass = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Code = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IncomeTypeId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    IsTaxable = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsHazardPay = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Allowances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Allowances_AllowanceTypes_IncomeTypeId",
                        column: x => x.IncomeTypeId,
                        principalTable: "AllowanceTypes",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Areas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Code = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Address = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Boundary = table.Column<Polygon>(type: "geometry", nullable: true)
                        .Annotation("MySql:SpatialReferenceSystemId", 4326),
                    BranchId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Areas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Areas_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ClientHolidays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ClientId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HolType = table.Column<int>(type: "int", nullable: false),
                    WorkType = table.Column<int>(type: "int", nullable: false),
                    HolYear = table.Column<int>(type: "int", nullable: false),
                    HolDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsRecuring = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsPaid = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientHolidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientHolidays_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DeductionApplicationDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DeductionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ApplicationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Principal = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Interest = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    RecordOrder = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Notes = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeductionApplicationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeductionApplicationDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeductionApplicationDetails_DeductionApplications_DeductionA~",
                        column: x => x.DeductionApplicationId,
                        principalTable: "DeductionApplications",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Deductions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CategoryId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Code = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PriorityLevel = table.Column<int>(type: "int", nullable: false),
                    AllowEmployeeFiling = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deductions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Deductions_DeductionTypes_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "DeductionTypes",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "leaveApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LeaveId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DurationType = table.Column<int>(type: "int", nullable: false),
                    LeaveDateFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    LeaveDateTo = table.Column<DateOnly>(type: "date", nullable: false),
                    DayFraction = table.Column<int>(type: "int", nullable: false),
                    PayType = table.Column<int>(type: "int", nullable: false),
                    PayoutMode = table.Column<int>(type: "int", nullable: false),
                    GovernmentAmount = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    CompanyAmount = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    ReleasePayrollDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EmployerAdvancesPayment = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    ReimbursementStatus = table.Column<int>(type: "int", nullable: false),
                    ReimbursementFiledDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReimbursementReceivedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReimbursementReferenceNo = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsManualEntry = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EndTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TotalMinutes = table.Column<double>(type: "double", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "int", nullable: false),
                    ReviewedBy = table.Column<int>(type: "int", nullable: true),
                    ReviewedOn = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ApplicationRemarks = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SupportingDocumentUrl = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AuditTrailId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leaveApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_leaveApplications_Leaves_LeaveId",
                        column: x => x.LeaveId,
                        principalTable: "Leaves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CutoffDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayrollGroupId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Day = table.Column<int>(type: "int", nullable: false),
                    IsEndOfMonth = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Label = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CutoffDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CutoffDays_PayrollGroups_PayrollGroupId",
                        column: x => x.PayrollGroupId,
                        principalTable: "PayrollGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OtherIncomeApplicationDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ApplicationId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IncomeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    IsTaxable = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsProrated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Notes = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConsumedByPayrollId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    OtherIncomeApplicationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtherIncomeApplicationDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OtherIncomeApplicationDetails_Allowances_IncomeId",
                        column: x => x.IncomeId,
                        principalTable: "Allowances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OtherIncomeApplicationDetails_OtherIncomeApplications_OtherI~",
                        column: x => x.OtherIncomeApplicationId,
                        principalTable: "OtherIncomeApplications",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Holidays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HolType = table.Column<int>(type: "int", nullable: false),
                    WorkType = table.Column<int>(type: "int", nullable: false),
                    HolYear = table.Column<int>(type: "int", nullable: false),
                    HolDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsRecuring = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    WeekOfMonth = table.Column<int>(type: "int", nullable: true),
                    DayOfWeek = table.Column<int>(type: "int", nullable: true),
                    IsPaid = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AreaId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Holidays_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AssignAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    AssetType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AssetDescription = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Model = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Brand = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SerialNo = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Qty = table.Column<int>(type: "int", nullable: false),
                    IssuanceDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReturnedDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Remarks = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    File = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignAssets", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Attendances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BioId = table.Column<int>(type: "int", nullable: true),
                    WorkDateTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IP = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeviceName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    DepartmentId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ClientId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    BranchId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    OperationAreaId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UserName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Workstate = table.Column<int>(type: "int", nullable: false),
                    Verifycode = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LogRemarks = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BatchCode = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EditRemarks = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LogSource = table.Column<int>(type: "int", nullable: false),
                    Boundary = table.Column<Polygon>(type: "geometry", nullable: true)
                        .Annotation("MySql:SpatialReferenceSystemId", 4326),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attendances_Areas_OperationAreaId",
                        column: x => x.OperationAreaId,
                        principalTable: "Areas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Attendances_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Attendances_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ChangeHolidays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BatchCode = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HolidayId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayrollDate = table.Column<DateOnly>(type: "date", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeHolidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeHolidays_Holidays_HolidayId",
                        column: x => x.HolidayId,
                        principalTable: "Holidays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ChangeRestDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DayName = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayrollDate = table.Column<DateOnly>(type: "date", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    BatchCode = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApprovalStatus = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeRestDays", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DailyTimeRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BatchCode = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WorkType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WorkTypeEnum = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ShiftId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ShiftName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ShiftStartTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ShiftEndTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EndTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LateMinutes = table.Column<double>(type: "double", nullable: false),
                    UTMinutes = table.Column<double>(type: "double", nullable: false),
                    OverMinutes = table.Column<double>(type: "double", nullable: false),
                    LateForOTMinutes = table.Column<double>(type: "double", nullable: false),
                    OBHours = table.Column<double>(type: "double", nullable: false),
                    AbsentCount = table.Column<int>(type: "int", nullable: false),
                    HolCount = table.Column<int>(type: "int", nullable: false),
                    SPCount = table.Column<int>(type: "int", nullable: false),
                    PaidLeaveHours = table.Column<double>(type: "double", nullable: false),
                    UnpaidLeaveHours = table.Column<double>(type: "double", nullable: false),
                    RegularNetHours = table.Column<double>(type: "double", nullable: false),
                    RegularOTHours = table.Column<double>(type: "double", nullable: false),
                    RegularNDHours = table.Column<double>(type: "double", nullable: false),
                    RegularNDOTHours = table.Column<double>(type: "double", nullable: false),
                    RestDayHours = table.Column<double>(type: "double", nullable: false),
                    RestDayOTHours = table.Column<double>(type: "double", nullable: false),
                    RestDayNDHours = table.Column<double>(type: "double", nullable: false),
                    RestDayNDOTHours = table.Column<double>(type: "double", nullable: false),
                    LegalHolHours = table.Column<double>(type: "double", nullable: false),
                    LegalHolOTHours = table.Column<double>(type: "double", nullable: false),
                    LegalHolNightDiffHours = table.Column<double>(type: "double", nullable: false),
                    LegalHolNightDiffOTHours = table.Column<double>(type: "double", nullable: false),
                    SpecialHolHours = table.Column<double>(type: "double", nullable: false),
                    SpecialHolOTHours = table.Column<double>(type: "double", nullable: false),
                    SpecialHolNightDiffHours = table.Column<double>(type: "double", nullable: false),
                    SpecialHolNightDiffOTHours = table.Column<double>(type: "double", nullable: false),
                    RestLegalDayHours = table.Column<double>(type: "double", nullable: false),
                    RestLegalDayOTHours = table.Column<double>(type: "double", nullable: false),
                    RestLegalDayNDHours = table.Column<double>(type: "double", nullable: false),
                    RestLegalDayNDOTHours = table.Column<double>(type: "double", nullable: false),
                    RestSpecialDayHours = table.Column<double>(type: "double", nullable: false),
                    RestSpecialDayOTHours = table.Column<double>(type: "double", nullable: false),
                    RestSpecialDayNDHours = table.Column<double>(type: "double", nullable: false),
                    RestSpecialDayNDOTHours = table.Column<double>(type: "double", nullable: false),
                    DoubleLegalHours = table.Column<double>(type: "double", nullable: false),
                    DoubleLegalOTHours = table.Column<double>(type: "double", nullable: false),
                    DoubleLegalNDHours = table.Column<double>(type: "double", nullable: false),
                    DoubleLegalNDOTHours = table.Column<double>(type: "double", nullable: false),
                    RestDoubleLegalHours = table.Column<double>(type: "double", nullable: false),
                    RestDoubleLegalOTHours = table.Column<double>(type: "double", nullable: false),
                    RestDoubleLegalNDHours = table.Column<double>(type: "double", nullable: false),
                    RestDoubleLegalNDOTHours = table.Column<double>(type: "double", nullable: false),
                    Note = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PostingDescription = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    BranchId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    DepartmentId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    PayrollGroupId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ClientId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    AreaId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ShiftWorkingHour = table.Column<double>(type: "double", nullable: false),
                    Posted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyTimeRecords", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DtrLeaveMetaDatas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DTRId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LeaveId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Hours = table.Column<double>(type: "double", nullable: false),
                    StartDateTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EndDateTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    PayType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DtrLeaveMetaDatas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DtrLeaveMetaDatas_DailyTimeRecords_DTRId",
                        column: x => x.DTRId,
                        principalTable: "DailyTimeRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Code = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HeadId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    BioId = table.Column<int>(type: "int", nullable: true),
                    Email = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EmployeeNo = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DepartmentId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    PayrollGroupId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ClientId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    AreaId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    BranchId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    SectionId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    PositionId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    JobLevel = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TimeShiftId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    DateRegistered = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    HireDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ContractStart = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ContractEnd = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CivilStatus = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DateResigned = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    HiringEntity = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FirstName = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastName = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MiddleName = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Suffix = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Gender = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Age = table.Column<int>(type: "int", nullable: true),
                    MonthlyRate = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    DailyRate = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Cola = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    DailyRateMode = table.Column<int>(type: "int", nullable: false),
                    FactorDays = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    UseActualMonthDays = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsRestDayPaid = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsRegularHolidayIncluded = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsSpecialNonWorkingIncluded = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    UseEmployeeOverride = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    DOB = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    BloodType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModeOfPayment = table.Column<int>(type: "int", nullable: false),
                    SalaryType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EmploymentStatus = table.Column<int>(type: "int", nullable: false),
                    BankName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BankNo = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SSSNo = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PHICNo = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HDMFNo = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TIN = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RDOCode = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Contact = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Address1 = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Address2 = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProfileImg = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employees_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Employees_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Employees_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Employees_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Employees_PayrollGroups_PayrollGroupId",
                        column: x => x.PayrollGroupId,
                        principalTable: "PayrollGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Employees_Positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "Positions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Employees_TimeShifts_TimeShiftId",
                        column: x => x.TimeShiftId,
                        principalTable: "TimeShifts",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Sections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DepartmentId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    SectionType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Code = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sections_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Dependents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    FullName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Relationship = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Gender = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DOB = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dependents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dependents_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Educations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    SchoolName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    YearGraduated = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Educations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Educations_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EmployeeFixedSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DayName = table.Column<int>(type: "int", nullable: false),
                    TimeShiftId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeFixedSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeFixedSchedules_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeFixedSchedules_TimeShifts_TimeShiftId",
                        column: x => x.TimeShiftId,
                        principalTable: "TimeShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EmployeeRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    RecordType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    File = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeRecords_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EmployeeSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    IsEligibleForOvertime = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsEligibleForRegularHolidayPay = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsEligibleForSpecialHolidayPay = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsEligibleForNightDifferential = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsEligibleForLeaveCredits = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsEligibleFor13thMonth = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsNoDTRNotRequired = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeSettings_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Employments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CompanyName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Position = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "HDMFRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ComputationType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EE = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    ER = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    AddOns = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HDMFRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HDMFRates_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LeaveCredits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PeriodYear = table.Column<int>(type: "int", nullable: false),
                    FromDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ToDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LeaveId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Granted = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Used = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Reserved = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveCredits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveCredits_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeaveCredits_Leaves_LeaveId",
                        column: x => x.LeaveId,
                        principalTable: "Leaves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OTApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OTDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EndTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ManualOTMinutes = table.Column<double>(type: "double", nullable: false),
                    IsManualEntry = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    OverTimeThreshold = table.Column<double>(type: "double", nullable: false),
                    Remarks = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApprovalStatus = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OTApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OTApplications_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PassSlipApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ApplicationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DepartureTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReturnTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Destination = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Purpose = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApprovalStatus = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BatchCode = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassSlipApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PassSlipApplications_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PayrollOpeningBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    BasicPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    OvertimePay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    HolidayPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Allowances = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    OtherIncome = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Bonuses = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    GrossIncome = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    NonTaxableIncome = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    SSSContribution = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    PhilHealthContribution = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    PagIbigContribution = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    WithholdingTax = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    OtherDeductions = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    NetPay = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollOpeningBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollOpeningBalances_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PHICRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ComputationType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EE = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    ER = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    AddOns = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PHICRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PHICRates_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PriorEmployerTaxRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    HasPriorEmployer = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PriorEmployerName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GrossIncomeYtd = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    NonTaxableYtd = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    StatutoryDeductionsYtd = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    TaxWithheldYtd = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriorEmployerTaxRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriorEmployerTaxRecords_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RestDayDates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayrollDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestDayDates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestDayDates_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RestDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DayName = table.Column<int>(type: "int", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestDays_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Level = table.Column<double>(type: "double", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Skills_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SSSRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ComputationType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EE = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    ER = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    EC = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    AddOns = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SSSRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SSSRates_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TaxRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ComputationType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EE = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    AddOns = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxRates_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ThirteenthMonthLedgers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayrollDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    Remarks = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThirteenthMonthLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThirteenthMonthLedgers_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "TravelOrderApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Reference = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApplicationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsManualEntry = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TotalMinutes = table.Column<double>(type: "double", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EndTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Destination = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Classification = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Purpose = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Cost = table.Column<double>(type: "double", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TravelOrderApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TravelOrderApplications_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UTApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PayrollDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UTMinutes = table.Column<double>(type: "double", nullable: false),
                    Remarks = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApprovalStatus = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UTApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UTApplications_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "WorkSchedulePlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BatchCode = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PayrollDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TimeShiftId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkSchedulePlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkSchedulePlans_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkSchedulePlans_TimeShifts_TimeShiftId",
                        column: x => x.TimeShiftId,
                        principalTable: "TimeShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LeaveLedgers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EmployeeId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LeaveId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    LeaveCreditsId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EntryType = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Add = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Less = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Particulars = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReferenceApplicationId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    DtrBatchCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveLedgers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveLedgers_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeaveLedgers_LeaveCredits_LeaveCreditsId",
                        column: x => x.LeaveCreditsId,
                        principalTable: "LeaveCredits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveLedgers_Leaves_LeaveId",
                        column: x => x.LeaveId,
                        principalTable: "Leaves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveLedgers_leaveApplications_ReferenceApplicationId",
                        column: x => x.ReferenceApplicationId,
                        principalTable: "leaveApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Allowances_IncomeTypeId",
                table: "Allowances",
                column: "IncomeTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Allowances_TenantId_DeletedAt",
                table: "Allowances",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AllowanceTypes_TenantId_DeletedAt",
                table: "AllowanceTypes",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Areas_BranchId",
                table: "Areas",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Areas_TenantId_DeletedAt",
                table: "Areas",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignAssets_EmployeeId",
                table: "AssignAssets",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignAssets_TenantId_DeletedAt",
                table: "AssignAssets",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Att_BRId_DepId_Area_ClId_LS",
                table: "Attendances",
                columns: new[] { "BranchId", "DepartmentId", "OperationAreaId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_LogSource_BatchCode",
                table: "Attendances",
                columns: new[] { "LogSource", "BatchCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_ls_wt",
                table: "Attendances",
                columns: new[] { "LogSource", "WorkDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_ClientId",
                table: "Attendances",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_EmployeeId",
                table: "Attendances",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_OperationAreaId",
                table: "Attendances",
                column: "OperationAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_TenantId_DeletedAt",
                table: "Attendances",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Branches_TenantId_DeletedAt",
                table: "Branches",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeHolidays_EmployeeId",
                table: "ChangeHolidays",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeHolidays_HolidayId",
                table: "ChangeHolidays",
                column: "HolidayId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeHolidays_TenantId_DeletedAt",
                table: "ChangeHolidays",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRestDays_EmployeeId_PayrollDate",
                table: "ChangeRestDays",
                columns: new[] { "EmployeeId", "PayrollDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeRestDays_TenantId_DeletedAt",
                table: "ChangeRestDays",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientBillingInfos_ClientId",
                table: "ClientBillingInfos",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientBillingInfos_TenantId_DeletedAt",
                table: "ClientBillingInfos",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientHolidays_ClientId",
                table: "ClientHolidays",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientHolidays_TenantId_DeletedAt",
                table: "ClientHolidays",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClientRateTables_ClientId_Type",
                table: "ClientRateTables",
                columns: new[] { "ClientId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientRateTables_TenantId_DeletedAt",
                table: "ClientRateTables",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_TenantId_DeletedAt",
                table: "Clients",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Companies_TenantId_DeletedAt",
                table: "Companies",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CutoffDays_PayrollGroupId",
                table: "CutoffDays",
                column: "PayrollGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CutoffDays_TenantId_DeletedAt",
                table: "CutoffDays",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyTimeRecords_EmployeeId_WorkDate",
                table: "DailyTimeRecords",
                columns: new[] { "EmployeeId", "WorkDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyTimeRecords_TenantId_DeletedAt",
                table: "DailyTimeRecords",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeductionApplicationDetails_DeductionApplicationId",
                table: "DeductionApplicationDetails",
                column: "DeductionApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_DeductionApplicationDetails_TenantId_DeletedAt",
                table: "DeductionApplicationDetails",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeductionApplications_TenantId_DeletedAt",
                table: "DeductionApplications",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeductionPayments_TenantId_DeletedAt",
                table: "DeductionPayments",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Deductions_CategoryId",
                table: "Deductions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Deductions_TenantId_DeletedAt",
                table: "Deductions",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DeductionTypes_TenantId_DeletedAt",
                table: "DeductionTypes",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Departments_HeadId",
                table: "Departments",
                column: "HeadId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_TenantId_DeletedAt",
                table: "Departments",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Dependents_EmployeeId",
                table: "Dependents",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Dependents_TenantId_DeletedAt",
                table: "Dependents",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DtrLeaveMetaDatas_DTRId",
                table: "DtrLeaveMetaDatas",
                column: "DTRId");

            migrationBuilder.CreateIndex(
                name: "IX_DtrLeaveMetaDatas_TenantId_DeletedAt",
                table: "DtrLeaveMetaDatas",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Educations_EmployeeId",
                table: "Educations",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Educations_TenantId_DeletedAt",
                table: "Educations",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeFixedSchedules_EmployeeId_DayName",
                table: "EmployeeFixedSchedules",
                columns: new[] { "EmployeeId", "DayName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeFixedSchedules_TenantId_DeletedAt",
                table: "EmployeeFixedSchedules",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeFixedSchedules_TimeShiftId",
                table: "EmployeeFixedSchedules",
                column: "TimeShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeRecords_EmployeeId",
                table: "EmployeeRecords",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeRecords_TenantId_DeletedAt",
                table: "EmployeeRecords",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_AreaId",
                table: "Employees",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_BranchId",
                table: "Employees",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ClientId",
                table: "Employees",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DepartmentId",
                table: "Employees",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_FirstName",
                table: "Employees",
                column: "FirstName");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_LastName",
                table: "Employees",
                column: "LastName");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_MiddleName",
                table: "Employees",
                column: "MiddleName");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_PayrollGroupId",
                table: "Employees",
                column: "PayrollGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_PositionId",
                table: "Employees",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_Suffix",
                table: "Employees",
                column: "Suffix");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId_DeletedAt",
                table: "Employees",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TimeShiftId",
                table: "Employees",
                column: "TimeShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSettings_EmployeeId",
                table: "EmployeeSettings",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSettings_TenantId_DeletedAt",
                table: "EmployeeSettings",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Employments_EmployeeId",
                table: "Employments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Employments_TenantId_DeletedAt",
                table: "Employments",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GeneralSettings_IdentityType_IdentityTypeId",
                table: "GeneralSettings",
                columns: new[] { "IdentityType", "IdentityTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_GeneralSettings_TenantId_DeletedAt",
                table: "GeneralSettings",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GovAnnualTaxes_TenantId_DeletedAt",
                table: "GovAnnualTaxes",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GovHDMFs_TenantId_DeletedAt",
                table: "GovHDMFs",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GovPHICs_TenantId_DeletedAt",
                table: "GovPHICs",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GovSSSes_TenantId_DeletedAt",
                table: "GovSSSes",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GovTaxes_TenantId_DeletedAt",
                table: "GovTaxes",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HDMFContributions_EmployeeId_PayrollDate",
                table: "HDMFContributions",
                columns: new[] { "EmployeeId", "PayrollDate" });

            migrationBuilder.CreateIndex(
                name: "IX_HDMFContributions_PayrollBatchId",
                table: "HDMFContributions",
                column: "PayrollBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_HDMFContributions_TenantId_DeletedAt",
                table: "HDMFContributions",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HDMFRates_EmployeeId",
                table: "HDMFRates",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HDMFRates_TenantId_DeletedAt",
                table: "HDMFRates",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_AreaId",
                table: "Holidays",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_TenantId_DeletedAt",
                table: "Holidays",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InboxState_Delivered",
                table: "InboxState",
                column: "Delivered");

            migrationBuilder.CreateIndex(
                name: "IX_IncomePayments_TenantId_DeletedAt",
                table: "IncomePayments",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_leaveApplications_LeaveId",
                table: "leaveApplications",
                column: "LeaveId");

            migrationBuilder.CreateIndex(
                name: "IX_leaveApplications_TenantId_DeletedAt",
                table: "leaveApplications",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveCredits_EmployeeId_LeaveId_PeriodYear",
                table: "LeaveCredits",
                columns: new[] { "EmployeeId", "LeaveId", "PeriodYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveCredits_LeaveId",
                table: "LeaveCredits",
                column: "LeaveId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveCredits_TenantId_DeletedAt",
                table: "LeaveCredits",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveLedgers_EmployeeId",
                table: "LeaveLedgers",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveLedgers_LeaveCreditsId",
                table: "LeaveLedgers",
                column: "LeaveCreditsId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveLedgers_LeaveId",
                table: "LeaveLedgers",
                column: "LeaveId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveLedgers_ReferenceApplicationId",
                table: "LeaveLedgers",
                column: "ReferenceApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveLedgers_TenantId_DeletedAt",
                table: "LeaveLedgers",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Leaves_TenantId_DeletedAt",
                table: "Leaves",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ManualAttendance_TenantId_DeletedAt",
                table: "ManualAttendance",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MinimumWageRates_TenantId_DeletedAt",
                table: "MinimumWageRates",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OTApplications_EmployeeId",
                table: "OTApplications",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_OTApplications_TenantId_DeletedAt",
                table: "OTApplications",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OtherIncomeApplicationDetails_IncomeId",
                table: "OtherIncomeApplicationDetails",
                column: "IncomeId");

            migrationBuilder.CreateIndex(
                name: "IX_OtherIncomeApplicationDetails_OtherIncomeApplicationId",
                table: "OtherIncomeApplicationDetails",
                column: "OtherIncomeApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_OtherIncomeApplicationDetails_TenantId_DeletedAt",
                table: "OtherIncomeApplicationDetails",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OtherIncomeApplications_TenantId_DeletedAt",
                table: "OtherIncomeApplications",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EnqueueTime",
                table: "OutboxMessage",
                column: "EnqueueTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_ExpirationTime",
                table: "OutboxMessage",
                column: "ExpirationTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
                table: "OutboxMessage",
                columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_OutboxId_SequenceNumber",
                table: "OutboxMessage",
                columns: new[] { "OutboxId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_Created",
                table: "OutboxState",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_PassSlipApplications_EmployeeId",
                table: "PassSlipApplications",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PassSlipApplications_TenantId_DeletedAt",
                table: "PassSlipApplications",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBatches_TenantId_DeletedAt",
                table: "PayrollBatches",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollGroups_TenantId_DeletedAt",
                table: "PayrollGroups",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollOpeningBalances_EmployeeId_Year",
                table: "PayrollOpeningBalances",
                columns: new[] { "EmployeeId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollOpeningBalances_TenantId_DeletedAt",
                table: "PayrollOpeningBalances",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Payrolls_TenantId_DeletedAt",
                table: "Payrolls",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PHICContributions_EmployeeId_PayrollDate",
                table: "PHICContributions",
                columns: new[] { "EmployeeId", "PayrollDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PHICContributions_PayrollBatchId",
                table: "PHICContributions",
                column: "PayrollBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PHICContributions_TenantId_DeletedAt",
                table: "PHICContributions",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PHICRates_EmployeeId",
                table: "PHICRates",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PHICRates_TenantId_DeletedAt",
                table: "PHICRates",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Positions_TenantId_DeletedAt",
                table: "Positions",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PremiumRates_TenantId_DeletedAt",
                table: "PremiumRates",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PriorEmployerTaxRecords_EmployeeId_Year",
                table: "PriorEmployerTaxRecords",
                columns: new[] { "EmployeeId", "Year" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriorEmployerTaxRecords_TenantId_DeletedAt",
                table: "PriorEmployerTaxRecords",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProratedAllowances_TenantId_DeletedAt",
                table: "ProratedAllowances",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RestDayDates_EmployeeId_PayrollDate",
                table: "RestDayDates",
                columns: new[] { "EmployeeId", "PayrollDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RestDayDates_TenantId_DeletedAt",
                table: "RestDayDates",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RestDays_EmployeeId",
                table: "RestDays",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_RestDays_TenantId_DeletedAt",
                table: "RestDays",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryAdjustments_TenantId_DeletedAt",
                table: "SalaryAdjustments",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Sections_DepartmentId",
                table: "Sections",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Sections_TenantId_DeletedAt",
                table: "Sections",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Skills_EmployeeId",
                table: "Skills",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_TenantId_DeletedAt",
                table: "Skills",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SSSContributions_EmployeeId_PayrollDate",
                table: "SSSContributions",
                columns: new[] { "EmployeeId", "PayrollDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SSSContributions_PayrollBatchId",
                table: "SSSContributions",
                column: "PayrollBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SSSContributions_TenantId_DeletedAt",
                table: "SSSContributions",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SSSRates_EmployeeId",
                table: "SSSRates",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SSSRates_TenantId_DeletedAt",
                table: "SSSRates",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxContributions_EmployeeId_PayrollDate",
                table: "TaxContributions",
                columns: new[] { "EmployeeId", "PayrollDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxContributions_PayrollBatchId",
                table: "TaxContributions",
                column: "PayrollBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxContributions_TenantId_DeletedAt",
                table: "TaxContributions",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TaxRates_EmployeeId",
                table: "TaxRates",
                column: "EmployeeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxRates_TenantId_DeletedAt",
                table: "TaxRates",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ThirteenthMonthLedgers_EmployeeId",
                table: "ThirteenthMonthLedgers",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ThirteenthMonthLedgers_TenantId_DeletedAt",
                table: "ThirteenthMonthLedgers",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TimeShifts_TenantId_DeletedAt",
                table: "TimeShifts",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TravelOrderApplications_EmployeeId",
                table: "TravelOrderApplications",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_TravelOrderApplications_TenantId_DeletedAt",
                table: "TravelOrderApplications",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UTApplications_EmployeeId",
                table: "UTApplications",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_UTApplications_TenantId_DeletedAt",
                table: "UTApplications",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedulePlans_EmployeeId_PayrollDate",
                table: "WorkSchedulePlans",
                columns: new[] { "EmployeeId", "PayrollDate" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedulePlans_TenantId_DeletedAt",
                table: "WorkSchedulePlans",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedulePlans_TimeShiftId",
                table: "WorkSchedulePlans",
                column: "TimeShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_YearLocks_TenantId_DeletedAt",
                table: "YearLocks",
                columns: new[] { "TenantId", "DeletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_YearLocks_TenantId_Year",
                table: "YearLocks",
                columns: new[] { "TenantId", "Year" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AssignAssets_Employees_EmployeeId",
                table: "AssignAssets",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Attendances_Employees_EmployeeId",
                table: "Attendances",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ChangeHolidays_Employees_EmployeeId",
                table: "ChangeHolidays",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChangeRestDays_Employees_EmployeeId",
                table: "ChangeRestDays",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyTimeRecords_Employees_EmployeeId",
                table: "DailyTimeRecords",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Employees_HeadId",
                table: "Departments",
                column: "HeadId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Areas_Branches_BranchId",
                table: "Areas");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Branches_BranchId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Employees_HeadId",
                table: "Departments");

            migrationBuilder.DropTable(
                name: "AssignAssets");

            migrationBuilder.DropTable(
                name: "Attendances");

            migrationBuilder.DropTable(
                name: "ChangeHolidays");

            migrationBuilder.DropTable(
                name: "ChangeRestDays");

            migrationBuilder.DropTable(
                name: "ClientBillingInfos");

            migrationBuilder.DropTable(
                name: "ClientHolidays");

            migrationBuilder.DropTable(
                name: "ClientRateTables");

            migrationBuilder.DropTable(
                name: "Companies");

            migrationBuilder.DropTable(
                name: "CutoffDays");

            migrationBuilder.DropTable(
                name: "DeductionApplicationDetails");

            migrationBuilder.DropTable(
                name: "DeductionPayments");

            migrationBuilder.DropTable(
                name: "Deductions");

            migrationBuilder.DropTable(
                name: "Dependents");

            migrationBuilder.DropTable(
                name: "DtrLeaveMetaDatas");

            migrationBuilder.DropTable(
                name: "Educations");

            migrationBuilder.DropTable(
                name: "EmployeeFixedSchedules");

            migrationBuilder.DropTable(
                name: "EmployeeRecords");

            migrationBuilder.DropTable(
                name: "EmployeeSettings");

            migrationBuilder.DropTable(
                name: "Employments");

            migrationBuilder.DropTable(
                name: "GeneralSettings");

            migrationBuilder.DropTable(
                name: "GovAnnualTaxes");

            migrationBuilder.DropTable(
                name: "GovHDMFs");

            migrationBuilder.DropTable(
                name: "GovPHICs");

            migrationBuilder.DropTable(
                name: "GovSSSes");

            migrationBuilder.DropTable(
                name: "GovTaxes");

            migrationBuilder.DropTable(
                name: "HDMFContributions");

            migrationBuilder.DropTable(
                name: "HDMFRates");

            migrationBuilder.DropTable(
                name: "InboxState");

            migrationBuilder.DropTable(
                name: "IncomePayments");

            migrationBuilder.DropTable(
                name: "LeaveLedgers");

            migrationBuilder.DropTable(
                name: "ManualAttendance");

            migrationBuilder.DropTable(
                name: "MinimumWageRates");

            migrationBuilder.DropTable(
                name: "OTApplications");

            migrationBuilder.DropTable(
                name: "OtherIncomeApplicationDetails");

            migrationBuilder.DropTable(
                name: "OutboxMessage");

            migrationBuilder.DropTable(
                name: "OutboxState");

            migrationBuilder.DropTable(
                name: "PassSlipApplications");

            migrationBuilder.DropTable(
                name: "PayrollBatches");

            migrationBuilder.DropTable(
                name: "PayrollOpeningBalances");

            migrationBuilder.DropTable(
                name: "Payrolls");

            migrationBuilder.DropTable(
                name: "PHICContributions");

            migrationBuilder.DropTable(
                name: "PHICRates");

            migrationBuilder.DropTable(
                name: "PremiumRates");

            migrationBuilder.DropTable(
                name: "PriorEmployerTaxRecords");

            migrationBuilder.DropTable(
                name: "ProratedAllowances");

            migrationBuilder.DropTable(
                name: "RestDayDates");

            migrationBuilder.DropTable(
                name: "RestDays");

            migrationBuilder.DropTable(
                name: "SalaryAdjustments");

            migrationBuilder.DropTable(
                name: "Sections");

            migrationBuilder.DropTable(
                name: "Skills");

            migrationBuilder.DropTable(
                name: "SSSContributions");

            migrationBuilder.DropTable(
                name: "SSSRates");

            migrationBuilder.DropTable(
                name: "TaxContributions");

            migrationBuilder.DropTable(
                name: "TaxRates");

            migrationBuilder.DropTable(
                name: "ThirteenthMonthLedgers");

            migrationBuilder.DropTable(
                name: "TravelOrderApplications");

            migrationBuilder.DropTable(
                name: "UTApplications");

            migrationBuilder.DropTable(
                name: "WorkSchedulePlans");

            migrationBuilder.DropTable(
                name: "YearLocks");

            migrationBuilder.DropTable(
                name: "Holidays");

            migrationBuilder.DropTable(
                name: "DeductionApplications");

            migrationBuilder.DropTable(
                name: "DeductionTypes");

            migrationBuilder.DropTable(
                name: "DailyTimeRecords");

            migrationBuilder.DropTable(
                name: "LeaveCredits");

            migrationBuilder.DropTable(
                name: "leaveApplications");

            migrationBuilder.DropTable(
                name: "Allowances");

            migrationBuilder.DropTable(
                name: "OtherIncomeApplications");

            migrationBuilder.DropTable(
                name: "Leaves");

            migrationBuilder.DropTable(
                name: "AllowanceTypes");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "Areas");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "PayrollGroups");

            migrationBuilder.DropTable(
                name: "Positions");

            migrationBuilder.DropTable(
                name: "TimeShifts");
        }
    }
}
