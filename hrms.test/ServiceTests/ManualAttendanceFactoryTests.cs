namespace hrms.test.ServiceTests;

/// <summary>ManualAttendanceFactory -- where a manually entered punch is recorded as having happened.</summary>
public class ManualAttendanceFactoryTests
{
    private static readonly ManualAttendanceEmployee Employee = new(
        Guid.NewGuid(), BioId: 7, BranchId: Guid.NewGuid(), DepartmentId: Guid.NewGuid(), ClientId: Guid.NewGuid(), AreaId: Guid.NewGuid());

    [Fact]
    public void LeftBlank_TheEmployeesOwnAssignmentIsUsed()
    {
        var entry = new CreateAttendance { EmployeeId = Employee.Id, WorkTime = new DateTime(2026, 9, 12, 8, 0, 0), Remarks = "Forgot to punch" };

        var log = ManualAttendanceFactory.Create(entry, Employee, "BATCH-1", _ => null);

        log.BranchId.Should().Be(Employee.BranchId);
        log.ClientId.Should().Be(Employee.ClientId);
        log.OperationAreaId.Should().Be(Employee.AreaId);
        log.DepartmentId.Should().Be(Employee.DepartmentId);
        log.LogSource.Should().Be(LOGSOURCE.MANUAL);
        log.EditRemarks.Should().Be("Forgot to punch");
        log.BioId.Should().Be(7);
        log.BatchCode.Should().Be("BATCH-1");
    }

    [Fact]
    public void Chosen_BranchClientAreaDepartment_OverrideTheEmployees()
    {
        Guid branch = Guid.NewGuid(), client = Guid.NewGuid(), area = Guid.NewGuid();
        var entry = new CreateAttendance
        {
            EmployeeId = Employee.Id, WorkTime = new DateTime(2026, 9, 12, 8, 0, 0), Remarks = "Relieving at another site",
            BranchId = branch, ClientId = client, OperationAreaId = area,
        };

        var log = ManualAttendanceFactory.Create(entry, Employee, "BATCH-1", _ => null);

        log.BranchId.Should().Be(branch);
        log.ClientId.Should().Be(client);
        log.OperationAreaId.Should().Be(area);
        log.DepartmentId.Should().Be(Employee.DepartmentId, "only what was chosen is overridden");
    }
}
