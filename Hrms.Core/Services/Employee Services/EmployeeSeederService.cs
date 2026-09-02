using Bogus;
using Hrms.Domain.Entities.EmployeeEntities;

namespace Hrms.Core.Services;

// Dev/testing utility: generates N realistic Employee records (with dependent SSS/PHIC/
// HDMF/Tax rate rows and eligibility settings) so payroll runs have something to compute
// against without hand-creating employees one by one. Reuses EmployeeImportService's
// AddOrUpdateRange + explicit SaveChanges/Commit pattern rather than the single-entity
// EmployeeService.AddAsync, since this always inserts a batch of brand-new employees.
public class EmployeeSeederService
{
    private const int MaxSeedCount = 500;
    private const string SeedPrefix = "SEED-";

    private readonly EmployeeService _employeeService;
    private readonly PayrollGroupService _payrollGroupService;
    private readonly DepartmentService _departmentService;
    private readonly ClientService _clientService;
    private readonly BranchService _branchService;

    public EmployeeSeederService(
        EmployeeService employeeService,
        PayrollGroupService payrollGroupService,
        DepartmentService departmentService,
        ClientService clientService,
        BranchService branchService)
    {
        _employeeService = employeeService;
        _payrollGroupService = payrollGroupService;
        _departmentService = departmentService;
        _clientService = clientService;
        _branchService = branchService;
    }

    public async Task<int> SeedAsync(int count, CancellationToken token)
    {
        if (count < 1 || count > MaxSeedCount)
            throw new ValidationException($"Count must be between 1 and {MaxSeedCount}.");

        var payrollGroups = await _payrollGroupService.FindAllAsync();
        if (payrollGroups.Count == 0)
            throw new ValidationException("No Payroll Groups exist yet — Employee.PayrollGroupId is required. Set up at least one Payroll Group first.");

        var departments = await _departmentService.FindAllAsync(token);
        var clients = await _clientService.FindAllAsync(token);
        var branches = await _branchService.FindAllAsync(token);

        var faker = new Faker("en");
        var stamp = DateTime.UtcNow.ToString("yyMMddHHmmss");
        var employees = new List<Employee>();

        for (var i = 0; i < count; i++)
        {
            var isMale = faker.Random.Bool();
            var firstName = faker.Name.FirstName(isMale ? Bogus.DataSets.Name.Gender.Male : Bogus.DataSets.Name.Gender.Female);
            var lastName = faker.Name.LastName();
            var salaryType = faker.Random.Bool() ? SalaryType.FIXED : SalaryType.VARIABLE;
            var monthlyRate = Math.Round(faker.Random.Decimal(18000, 60000), 2);
            var payrollGroup = faker.Random.ListItem(payrollGroups);

            var employee = new Employee
            {
                EmployeeNo = $"{SeedPrefix}{stamp}-{i + 1:D4}",
                FirstName = firstName,
                LastName = lastName,
                MiddleName = faker.Name.LastName(),
                Gender = isMale ? "Male" : "Female",
                CivilStatus = faker.PickRandom("Single", "Married", "Widowed", "Separated"),
                DOB = faker.Date.Past(30, DateTime.UtcNow.AddYears(-22)),
                Email = faker.Internet.Email(firstName, lastName),
                Contact = faker.Phone.PhoneNumber("09#########"),
                Address1 = faker.Address.FullAddress(),
                HireDate = DateOnly.FromDateTime(faker.Date.Past(3)),
                JobLevel = faker.PickRandom<JobLevelOption>(),
                EmploymentStatus = EmploymentStatus.Regular,
                ModeOfPayment = PaymentMethod.ATM,
                SalaryType = salaryType,
                MonthlyRate = salaryType == SalaryType.FIXED ? monthlyRate : 0,
                DailyRate = Math.Round(monthlyRate / 26, 2),
                DailyRateMode = DailyRateMode.Manual,
                PayrollGroupId = payrollGroup.Id,
                DepartmentId = departments.Count > 0 ? faker.Random.ListItem(departments).Id : null,
                ClientId = clients.Count > 0 ? faker.Random.ListItem(clients).Id : null,
                BranchId = branches.Count > 0 ? faker.Random.ListItem(branches).Id : null,
                BankName = faker.PickRandom("BDO", "BPI", "Metrobank", "Landbank"),
                BankNo = faker.Finance.Account(10),
                SSSNo = faker.Random.Replace("##-#######-#"),
                PHICNo = faker.Random.Replace("##-#########-#"),
                HDMFNo = faker.Random.Replace("####-####-####"),
                TIN = faker.Random.Replace("###-###-###-###"),
                SSSRate = new SSSRate { ComputationType = ComputationBasis.Table },
                PHICRate = new PHICRate { ComputationType = ComputationBasis.Table },
                HDMFRate = new HDMFRate { ComputationType = ComputationBasis.Table },
                TaxRate = new TaxRate { ComputationType = ComputationBasis.Table },
                Settings = new EmployeeSetting
                {
                    IsEligibleForOvertime = true,
                    IsEligibleForRegularHolidayPay = true,
                    IsEligibleForSpecialHolidayPay = true,
                    IsEligibleForNightDifferential = true,
                    IsEligibleForLeaveCredits = true,
                    IsEligibleFor13thMonth = true,
                },
            };
            employees.Add(employee);
        }

        await _employeeService.AddOrUpdateRange(employees, token);
        await _employeeService.SaveChangesAsync(token);
        await _employeeService.CommitChangesAsync(token);

        return employees.Count;
    }

    public async Task<int> RemoveSeededAsync(CancellationToken token)
    {
        var resp = await _employeeService.RemoveByEmployeeNoPrefixAsync(SeedPrefix, token);
        await _employeeService.CommitChangesAsync(token);
        return resp;
    }
}
