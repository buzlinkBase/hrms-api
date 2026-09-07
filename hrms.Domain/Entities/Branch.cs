using NetTopologySuite.Geometries;

namespace Hrms.Domain.Entities;


public class Branch : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Contact { get; set; }
    public string? ManagerName { get; set; }
    public string? Email { get; set; } = string.Empty;
    public Polygon? Boundary { get; set; }
    // DOLE regional wage order region (NCR, CAR, Region I-XIII, BARMM) — used to look up the
    // applicable MinimumWageRate for employees at this branch. See
    // PayrollReportService.GetMonthlyRemittanceReturnAsync.
    public string? RegionCode { get; set; }
    // Sector/class this establishment is registered under (e.g. "Non-Agriculture",
    // "Retail/Service establishments employing 10 workers or less") — matched against
    // MinimumWageRate.WageOrderClass for the same RegionCode. Null = unspecified, falls
    // back to the region's general (class-less) rate. See
    // PayrollReportService.ResolveRegionRate.
    public string? WageOrderClass { get; set; }

}

