using Hrms.Core.Services.OrgChart;
using Hrms.Domain.ValueObjects;

namespace hrms.test.ServiceTests;

/// <summary>
/// The org chart reads Company > Department > Section > people, nests people under a manager who
/// is in the same unit, orders by position level, and never duplicates or hangs on bad data.
/// </summary>
public class OrgChartBuilderTests
{
    private static OrgPerson Person(string name, Guid? dept = null, Guid? section = null, Guid? manager = null, int level = 0, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), name, "Staff", level, manager, dept, section);

    private static OrgNodeModel Company(IReadOnlyCollection<OrgDepartment> d, IReadOnlyCollection<OrgSection> s, IReadOnlyCollection<OrgPerson> p) =>
        OrgChartBuilder.Build("Acme", d, s, p).Single();

    private static IEnumerable<OrgNodeModel> Flatten(OrgNodeModel n) => new[] { n }.Concat(n.Children.SelectMany(Flatten));

    [Fact]
    public void Build_PlacesPeopleUnderTheirSectionThenDepartment()
    {
        var dept = new OrgDepartment(Guid.NewGuid(), "Ops", null, null);
        var section = new OrgSection(Guid.NewGuid(), "Night", dept.Id, null);
        var inSection = Person("Sam", dept.Id, section.Id);
        var inDept = Person("Dee", dept.Id);

        var company = Company([dept], [section], [inSection, inDept]);

        var ops = company.Children.Single();
        ops.Kind.Should().Be(OrgNodeKinds.Department);
        ops.Children.Select(c => c.Name).Should().BeEquivalentTo(["Dee", "Night"]);
        ops.Children.Single(c => c.Kind == OrgNodeKinds.Section).Children.Single().Name.Should().Be("Sam");
        company.HeadCount.Should().Be(2);
    }

    [Fact]
    public void Build_NestsSubDepartmentsAndSurvivesParentLoops()
    {
        var division = new OrgDepartment(Guid.NewGuid(), "Division", null, null);
        var child = new OrgDepartment(Guid.NewGuid(), "Child", division.Id, null);
        var loopA = new OrgDepartment(Guid.NewGuid(), "A", null, null);
        var loopB = new OrgDepartment(Guid.NewGuid(), "B", loopA.Id, null);
        var loopedA = loopA with { ParentId = loopB.Id };

        var company = Company([division, child, loopedA, loopB], [], []);

        company.Children.Select(c => c.Name).Should().BeEquivalentTo(["A", "B", "Division"]);
        company.Children.Single(c => c.Name == "Division").Children.Single().Name.Should().Be("Child");
    }

    [Fact]
    public void Build_PutsTheHeadAtTheTopOfTheUnitAndOrdersTopsByLevel()
    {
        var headId = Guid.NewGuid();
        var dept = new OrgDepartment(Guid.NewGuid(), "Sales", null, headId);
        var head = Person("Hana", dept.Id, id: headId, level: 9);
        var junior = Person("Zed", dept.Id, level: 1);
        var senior = Person("Amy", dept.Id, level: 5);

        var sales = Company([dept], [], [junior, head, senior]).Children.Single();

        var top = sales.Children.Single();
        top.Name.Should().Be("Hana");
        top.Children.Select(c => c.Name).Should().Equal("Amy", "Zed");
        top.HeadCount.Should().Be(3);
    }

    [Fact]
    public void Build_NestsUnderAManagerInTheSameUnitButNotAcrossUnits()
    {
        var a = new OrgDepartment(Guid.NewGuid(), "A", null, null);
        var b = new OrgDepartment(Guid.NewGuid(), "B", null, null);
        var boss = Person("Boss", a.Id);
        var sameUnit = Person("Same", a.Id, manager: boss.Id);
        var otherUnit = Person("Other", b.Id, manager: boss.Id);

        var company = Company([a, b], [], [boss, sameUnit, otherUnit]);

        var unitA = company.Children.Single(c => c.Name == "A");
        unitA.Children.Single().Children.Single().Name.Should().Be("Same");
        company.Children.Single(c => c.Name == "B").Children.Single().Name.Should().Be("Other");
    }

    [Fact]
    public void Build_SurvivesManagerCyclesAndPlacesEveryoneOnce()
    {
        var dept = new OrgDepartment(Guid.NewGuid(), "D", null, null);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var people = new[] { Person("A", dept.Id, manager: b, id: a), Person("B", dept.Id, manager: a, id: b) };

        var company = Company([dept], [], people);

        Flatten(company).Where(n => n.Kind == OrgNodeKinds.Employee).Select(n => n.Id)
            .Should().BeEquivalentTo([a.ToString(), b.ToString()]);
    }

    [Fact]
    public void Build_ADepartmentHeadWhoAlsoHeadsASectionStaysAtTheDepartment()
    {
        var headId = Guid.NewGuid();
        var dept = new OrgDepartment(Guid.NewGuid(), "D", null, headId);
        var section = new OrgSection(Guid.NewGuid(), "S", dept.Id, headId);
        var head = Person("Hana", dept.Id, section.Id, id: headId);

        var company = Company([dept], [section], [head]);

        Flatten(company).Count(n => n.Id == headId.ToString()).Should().Be(1);
        company.Children.Single().Children.Single().Name.Should().Be("Hana");
    }

    [Fact]
    public void Build_PeopleWithoutAUsableDepartmentGoUnderUnassigned()
    {
        var dept = new OrgDepartment(Guid.NewGuid(), "D", null, null);
        var homeless = Person("Hal");
        var ghostDept = Person("Gus", Guid.NewGuid());

        var company = Company([dept], [], [homeless, ghostDept]);

        var unassigned = company.Children.Single(c => c.Name == OrgChartBuilder.UnassignedName);
        unassigned.Children.Select(c => c.Name).Should().BeEquivalentTo(["Gus", "Hal"]);
    }
}
