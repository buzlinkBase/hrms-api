namespace hrms.test.ServiceTests;

/// <summary>
/// AttendanceLocationResolver -- the branch/client/project site/department every attendance log is
/// stored with: what the source set, blanks from the employee, never mixing branches.
/// </summary>
public class AttendanceLocationResolverTests
{
    private static readonly Guid Manila = Guid.NewGuid(), Cebu = Guid.NewGuid();
    private static readonly Guid ManilaSite = Guid.NewGuid(), CebuSite = Guid.NewGuid(), UnassignedSite = Guid.NewGuid();
    private static readonly Guid Client = Guid.NewGuid(), OtherClient = Guid.NewGuid(), Dept = Guid.NewGuid();

    private static Guid? BranchOf(Guid site) =>
        site == ManilaSite ? Manila : site == CebuSite ? Cebu : null;

    // A Manila employee at the Manila site.
    private static readonly AttendanceLocation Employee = new(Manila, Client, ManilaSite, Dept);

    [Fact]
    public void NothingChosen_TakesTheEmployeesOwn()
    {
        AttendanceLocationResolver.Resolve(AttendanceLocation.None, Employee, BranchOf)
            .Should().Be(Employee);
    }

    [Fact]
    public void ChosenValues_Win()
    {
        var chosen = new AttendanceLocation(Cebu, OtherClient, CebuSite, null);

        AttendanceLocationResolver.Resolve(chosen, Employee, BranchOf)
            .Should().Be(new AttendanceLocation(Cebu, OtherClient, CebuSite, Dept));
    }

    [Fact]
    public void ChosenBranch_DoesNotPickUpTheEmployeesSiteFromAnotherBranch()
    {
        var chosen = new AttendanceLocation(Cebu, null, null, null);

        AttendanceLocationResolver.Resolve(chosen, Employee, BranchOf)
            .Should().Be(new AttendanceLocation(Cebu, Client, null, Dept), "a Cebu punch is never tagged to a Manila site");
    }

    [Fact]
    public void ChosenBranch_KeepsTheEmployeesSiteInThatBranch()
    {
        var chosen = new AttendanceLocation(Manila, null, null, null);

        AttendanceLocationResolver.Resolve(chosen, Employee, BranchOf)
            .Should().Be(Employee);
    }

    [Fact]
    public void ChosenBranch_KeepsAnEmployeeSiteThatHasNoBranch()
    {
        var employee = Employee with { OperationAreaId = UnassignedSite };

        AttendanceLocationResolver.Resolve(new AttendanceLocation(Cebu, null, null, null), employee, BranchOf)
            .OperationAreaId.Should().Be(UnassignedSite);
    }

    [Fact]
    public void ChosenSite_WithoutABranch_PlacesThePunchInTheSitesBranch()
    {
        var chosen = new AttendanceLocation(null, null, CebuSite, null);

        AttendanceLocationResolver.Resolve(chosen, Employee, BranchOf)
            .Should().Be(new AttendanceLocation(Cebu, Client, CebuSite, Dept));
    }

    [Fact]
    public void UnregisteredEmployee_KeepsOnlyWhatTheSourceSet()
    {
        var chosen = new AttendanceLocation(Cebu, null, null, null);

        AttendanceLocationResolver.Resolve(chosen, null, BranchOf)
            .Should().Be(chosen);
    }
}
