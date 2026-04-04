
using Hrms.Domain.Entities;
namespace Hrms.Core.Services;

public class AccountInitService : BaseService<Company>
{
    private readonly ITenantProvider _tenantProvider;

    public AccountInitService(IUnitOfWorkService uow,
        ITenantProvider tenantProvider) : base(uow)
    {
        _tenantProvider = tenantProvider;
    }
    public async Task Create(CancellationToken token)
    {
        //await CreateOrUpdateAsync(company); 
        SetDefaultLeaves();
        SetDefaultRates();
        //set default branch
        //set default department
        await CommitChangesAsync(token);
    }

    private void SetDefaultLeaves()
    {
        var leaves = new List<Leave>
            {
                new Leave { Id = Guid.NewGuid(), Code = "SIL", Description = "Service Incentive Leave (SIL)", PaySource = PaySource.Company, Credits = 5, LeaveReset = LeaveReset.PerPeriod },
                new Leave { Id = Guid.NewGuid(), Code = "PL", Description = "Paternity Leave", PaySource = PaySource.Company, Credits = 7, LeaveReset = LeaveReset.PerEvent },
                new Leave { Id = Guid.NewGuid(), Code = "SPL", Description = "Parental Leave for Solo Parents", PaySource = PaySource.Company, Credits = 7, LeaveReset = LeaveReset.PerPeriod },
                new Leave { Id = Guid.NewGuid(), Code = "SLW", Description = "Special Leave for Women (Gynecological Disorders)", PaySource = PaySource.Company, Credits = 60, LeaveReset = LeaveReset.PerEvent },
                new Leave { Id = Guid.NewGuid(), Code = "VAWC", Description = "Leave for Victims of Violence Against Women and Children", PaySource = PaySource.Company, Credits = 10, LeaveReset = LeaveReset.PerEvent },
                new Leave { Id = Guid.NewGuid(), Code = "RL", Description = "Rehabilitation Leave (Occupational Injuries)", PaySource = PaySource.Company, Credits = 120, LeaveReset = LeaveReset.PerEvent },
                new Leave { Id = Guid.NewGuid(), Code = "SEL", Description = "Special Emergency Leave (Calamities)", PaySource = PaySource.Company, Credits = 5, LeaveReset = LeaveReset.PerEvent },
                new Leave { Id = Guid.NewGuid(), Code = "VL", Description = "Vacation Leave", PaySource = PaySource.Company, Credits = 10, LeaveReset = LeaveReset.PerPeriod },
                new Leave { Id = Guid.NewGuid(), Code = "SL", Description = "Sick Leave", PaySource = PaySource.Company, Credits = 5, LeaveReset = LeaveReset.PerPeriod },
                new Leave { Id = Guid.NewGuid(), Code = "EL", Description = "Educational Leave (Government Employees)", PaySource = PaySource.Government, Credits = 10, LeaveReset = LeaveReset.PerPeriod },
                new Leave { Id = Guid.NewGuid(), Code = "MC", Description = "Magna Carta Leave for Government Employees", PaySource = PaySource.Government, Credits = 5, LeaveReset = LeaveReset.PerPeriod },
                new Leave { Id = Guid.NewGuid(), Code = "ML", Description = "Maternity Leave", PaySource = PaySource.Government, Credits = 105, LeaveReset = LeaveReset.PerEvent },
            };
        _uow.Repository.AddRange(leaves);
    }
    private void SetDefaultRates()
    {
        var rates = new List<RateTable>
    {
        // Base Rates
        new RateTable { Id = Guid.NewGuid(), Type = RateType.REGULAR, ShortDescription = "REG", Description = "Regular Day", Rate = RATE_DEFAULT.REGULAR },
        new RateTable { Id = Guid.NewGuid(), Type = RateType.NIGHTDIFF, ShortDescription = "ND", Description = "Night Differential", Rate = RATE_DEFAULT.NIGHTDIFF },
        new RateTable { Id = Guid.NewGuid(), Type = RateType.OVERTIME, ShortDescription = "OT", Description = "Overtime", Rate = RATE_DEFAULT.OVERTIME },
        new RateTable { Id = Guid.NewGuid(), Type = RateType.RESTDAY_DUTY, ShortDescription = "RD", Description = "Rest Day Duty", Rate = RATE_DEFAULT.RESTDAY_DUTY },
        new RateTable { Id = Guid.NewGuid(), Type = RateType.LEGAL_HOLIDAY, ShortDescription = "LH", Description = "Legal Holiday (No Work)", Rate = RATE_DEFAULT.LEGAL_HOLIDAY },
        new RateTable { Id = Guid.NewGuid(), Type = RateType.LEGAL_HOLIDAY_DUTY, ShortDescription = "LH-DUTY", Description = "Legal Holiday Duty", Rate = RATE_DEFAULT.LEGAL_HOLIDAY_DUTY },
        new RateTable { Id = Guid.NewGuid(), Type = RateType.SPECIAL_WORKING, ShortDescription = "SP-WH", Description = "Special Working Holiday", Rate = RATE_DEFAULT.SPECIAL_WORKING },
        new RateTable { Id = Guid.NewGuid(), Type = RateType.SPECIAL_NON_WORKING, ShortDescription = "SP-NWH", Description = "Special Non-Working Holiday", Rate = RATE_DEFAULT.SPECIAL_NON_WORKING },
        new RateTable { Id = Guid.NewGuid(), Type = RateType.RESTDAY_SPECIAL, ShortDescription = "RD-SP", Description = "Special Rest Day", Rate = RATE_DEFAULT.RESTDAY_SPECIAL }
    };
        _uow.Repository.AddRange(rates);
    }
}
