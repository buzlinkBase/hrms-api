using hrms.test.TestSupport;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using Mapster;

namespace hrms.test.ServiceTests;

// IncomeAplMainService builds the schedule rows itself (stamping IncomeId/
// EmployeeId). If Mapster also copied payload.Schedule, every save inserted extra rows with an
// empty IncomeId and failed the FK to Allowances.
public class OtherIncomeApplicationMappingTests
{
    private static readonly TypeAdapterConfig Config = TestMapping.GlobalSettings;

    [Fact]
    public void Create_payload_does_not_map_schedule_rows()
    {
        var payload = new CreateOtherIncomeApplication
        {
            IncomeId = Guid.NewGuid(),
            Schedule = new List<CreateOtherIncomeSchedule>
            {
                new() { Date = new DateOnly(2026, 9, 1), Amount = 500 },
            },
        };

        var model = payload.Adapt<OtherIncomeApplication>(Config);

        Assert.Empty(model.Schedule);
        Assert.Equal(payload.IncomeId, model.IncomeId);
    }

    [Fact]
    public void Update_payload_does_not_map_schedule_rows()
    {
        var payload = new UpdateOtherIncomeApplication
        {
            Id = Guid.NewGuid(),
            IncomeId = Guid.NewGuid(),
            Schedule = new List<UpdateOtherIncomeSchedule>
            {
                new() { Id = Guid.NewGuid(), Date = new DateOnly(2026, 9, 1), Amount = 500 },
            },
        };

        var model = payload.Adapt<OtherIncomeApplication>(Config);

        Assert.Empty(model.Schedule);
    }
}
