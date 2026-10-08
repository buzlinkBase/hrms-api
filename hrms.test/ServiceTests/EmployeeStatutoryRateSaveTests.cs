using Hrms.Core.Services;
using Hrms.Domain;
using Hrms.Domain.Entities;
using Hrms.Domain.Entities.EmployeeEntities;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Hrms.Domain.ValueObjects;
using hrms.test.TestSupport;
using Mapster;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace hrms.test.ServiceTests;

/// <summary>
/// Employee &gt; Gov't IDs &amp; Rates: the names the API uses for the SSS/PHIC/HDMF/tax rate values,
/// which the Employee form has to read back after Save.
/// </summary>
public class EmployeeStatutoryRateSaveTests
{
    // The API serializes with Newtonsoft's camel-casing (ServiceRegitrations.AddNewtonsoftJson), which
    // lower-cases a leading run of capitals: EE -> "ee", ER -> "er", EC -> "ec", AddOns -> "addOns".
    // The Employee form used to read "eE"/"eR"/"eC", so after a reload only Add-ons came back and the
    // EE/ER/EC boxes showed 0 (the next Save then stored those zeros).
    [Fact]
    public void RatesAreSentOverTheWireAsLowercaseEeErEcAndCamelCaseAddOns()
    {
        var settings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };

        var sss = JObject.Parse(JsonConvert.SerializeObject(new CreateSSSRate { EE = 1, ER = 2, EC = 3, AddOns = 4 }, settings));
        var phic = JObject.Parse(JsonConvert.SerializeObject(new CreatePHICRate { EE = 1, ER = 2, AddOns = 6 }, settings));
        var tax = JObject.Parse(JsonConvert.SerializeObject(new CreateTaxRate { EE = 1, AddOns = 2 }, settings));

        sss.Properties().Select(p => p.Name).Should().BeEquivalentTo("computationType", "ee", "er", "ec", "addOns");
        phic.Properties().Select(p => p.Name).Should().BeEquivalentTo("computationType", "ee", "er", "addOns");
        tax.Properties().Select(p => p.Name).Should().Contain(["ee", "addOns"]);
    }

    [Fact]
    public void TheFormsCamelCasedKeysStillBindOnTheWayIn()
    {
        // The form posts "eE"/"eR"/"eC"; Newtonsoft matches property names case-insensitively.
        var sss = JsonConvert.DeserializeObject<CreateSSSRate>("{\"computationType\":\"FixedPerPayroll\",\"eE\":500,\"eR\":1000,\"eC\":10,\"addOns\":4}")!;

        (sss.EE, sss.ER, sss.EC, sss.AddOns).Should().Be((500m, 1000m, 10m, 4m));
    }

    private static Employee StoredEmployee()
    {
        var id = Guid.NewGuid();
        return new Employee
        {
            Id = id, FirstName = "Ana", LastName = "Reyes",
            SSSRate = new SSSRate { Id = Guid.NewGuid(), EmployeeId = id, ComputationType = ComputationBasis.Table },
            PHICRate = new PHICRate { Id = Guid.NewGuid(), EmployeeId = id, ComputationType = ComputationBasis.Table },
            HDMFRate = new HDMFRate { Id = Guid.NewGuid(), EmployeeId = id, ComputationType = ComputationBasis.Table },
            TaxRate = new TaxRate { Id = Guid.NewGuid(), EmployeeId = id, ComputationType = ComputationBasis.Table },
        };
    }

    private static UpdateEmployee TypedForm(Guid id) => new()
    {
        Id = id, FirstName = "Ana", LastName = "Reyes",
        SSSRate = new CreateSSSRate { ComputationType = ComputationBasis.FixedPerPayroll, EE = 500, ER = 1000, EC = 10, AddOns = 4 },
        PHICRate = new CreatePHICRate { ComputationType = ComputationBasis.FixedPerPayroll, EE = 250, ER = 250, AddOns = 6 },
        HDMFRate = new CreateHDMFRate { ComputationType = ComputationBasis.FixedPerPayroll, EE = 200, ER = 200, AddOns = 9 },
        TaxRate = new CreateTaxRate { ComputationType = ComputationBasis.FixedPerPayroll, EE = 300, AddOns = 11 },
    };

    [Fact]
    public void SavingTheForm_PutsEveryTypedRateValueOntoTheEmployee()
    {
        _ = TestMapping.GlobalSettings;
        var employee = StoredEmployee();

        TypedForm(employee.Id).Adapt(employee);

        (employee.SSSRate!.EE, employee.SSSRate.ER, employee.SSSRate.EC, employee.SSSRate.AddOns).Should().Be((500m, 1000m, 10m, 4m));
        (employee.PHICRate!.EE, employee.PHICRate.ER, employee.PHICRate.AddOns).Should().Be((250m, 250m, 6m));
        (employee.HDMFRate!.EE, employee.HDMFRate.ER, employee.HDMFRate.AddOns).Should().Be((200m, 200m, 9m));
        (employee.TaxRate!.EE, employee.TaxRate.AddOns).Should().Be((300m, 11m));
    }

    [Fact]
    public void AStoredEmployee_IsSentBackWithEveryRateValue()
    {
        _ = TestMapping.GlobalSettings;
        var employee = StoredEmployee();
        TypedForm(employee.Id).Adapt(employee);

        var model = employee.Adapt<EmployeeModel>();
        var json = JObject.Parse(JsonConvert.SerializeObject(model,
            new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }));

        json["sssRate"]!["ee"]!.Value<decimal>().Should().Be(500);
        json["sssRate"]!["er"]!.Value<decimal>().Should().Be(1000);
        json["sssRate"]!["ec"]!.Value<decimal>().Should().Be(10);
        json["sssRate"]!["addOns"]!.Value<decimal>().Should().Be(4);
        json["phicRate"]!["ee"]!.Value<decimal>().Should().Be(250);
        json["hdmfRate"]!["er"]!.Value<decimal>().Should().Be(200);
        json["taxRate"]!["ee"]!.Value<decimal>().Should().Be(300);
    }

    [Fact]
    public void TheFormsExactJson_BindsToUpdateEmployee_AndLandsOnTheStoredRates()
    {
        _ = TestMapping.GlobalSettings;
        var employee = StoredEmployee();
        // What the Employee form posts: camelCase keys, the rate boxes as eE/eR/eC.
        var json = """
        {
          "firstName":"Ana","lastName":"Reyes","sssNo":"06-3432188-3","tin":"395-823-919-000","rdoCode":"044",
          "sssRate":{"computationType":"FixedPerPayroll","eE":500,"eR":1000,"eC":10,"addOns":4},
          "phicRate":{"computationType":"FixedPerPayroll","eE":250,"eR":250,"addOns":6},
          "hdmfRate":{"computationType":"FixedPerPayroll","eE":200,"eR":200,"addOns":9},
          "taxRate":{"computationType":"FixedPerPayroll","eE":300,"addOns":11}
        }
        """;
        var settings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };

        var payload = JsonConvert.DeserializeObject<UpdateEmployee>(json, settings)!;
        payload.Adapt(employee);

        payload.SSSRate.Should().NotBeNull();
        (employee.SSSRate!.EE, employee.SSSRate.ER, employee.SSSRate.EC, employee.SSSRate.AddOns).Should().Be((500m, 1000m, 10m, 4m));
        (employee.PHICRate!.EE, employee.PHICRate.ER, employee.PHICRate.AddOns).Should().Be((250m, 250m, 6m));
        (employee.HDMFRate!.EE, employee.HDMFRate.ER, employee.HDMFRate.AddOns).Should().Be((200m, 200m, 9m));
        (employee.TaxRate!.EE, employee.TaxRate.AddOns).Should().Be((300m, 11m));
        (employee.SSSNo, employee.TIN, employee.RDOCode).Should().Be(("06-3432188-3", "395-823-919-000", "044"));
    }

    // ---- saving through EmployeeService against a production-like context ----
    // EmployeeService.UpdateAsync loads the employee without Include-ing the rate rows; it relies on
    // lazy loading (UseLazyLoadingProxies) to fetch each existing row while the form is applied.

    private static readonly Guid Group = Guid.NewGuid();

    private static EmployeeService BuildService(IUnitOfWorkService uow)
    {
        _ = TestMapping.GlobalSettings;
        return new EmployeeService(
            uow, Substitute.For<IMapper>(), TypeAdapterConfig.GlobalSettings,
            new DepartmentService(uow), new PayrollGroupService(uow), new BranchService(uow),
            new CostCenterService(uow), new PositionService(uow), new SectionService(uow));
    }

    private static async Task<Employee> SeedWithRatesAsync(SqliteHrmsContext db)
    {
        await using var seed = db.NewContext();
        seed.PayrollGroups.Add(new PayrollGroup { Id = Group });
        var employee = StoredEmployee();
        employee.PayrollGroupId = Group;
        seed.Employees.Add(employee);
        await seed.SaveChangesAsync();
        return employee;
    }

    [Fact]
    public async Task UpdateAsync_OnAProductionLikeContext_StoresEveryTypedRateOnTheExistingRows()
    {
        using var db = new SqliteHrmsContext();
        var employee = await SeedWithRatesAsync(db);
        var form = TypedForm(employee.Id);
        form.PayrollGroupId = Group;

        await using (var context = db.NewProductionLikeContext())
            await BuildService(new UnitOfWorkService(context)).UpdateAsync(form, CancellationToken.None);

        await using var check = db.NewContext();
        var sss = await check.SSSRates.IgnoreQueryFilters().Where(x => x.EmployeeId == employee.Id).ToListAsync();
        sss.Where(x => x.DeletedAt == null).Should().ContainSingle("the employee keeps one live SSS row");
        var live = sss.Single(x => x.DeletedAt == null);
        (live.EE, live.ER, live.EC, live.AddOns).Should().Be((500m, 1000m, 10m, 4m));
        var phic = await check.PHICRates.SingleAsync(x => x.EmployeeId == employee.Id);
        (phic.EE, phic.ER, phic.AddOns).Should().Be((250m, 250m, 6m));
        var hdmf = await check.HDMFRates.SingleAsync(x => x.EmployeeId == employee.Id);
        (hdmf.EE, hdmf.ER, hdmf.AddOns).Should().Be((200m, 200m, 9m));
        var tax = await check.TaxRates.SingleAsync(x => x.EmployeeId == employee.Id);
        (tax.EE, tax.AddOns).Should().Be((300m, 11m));
    }
}
