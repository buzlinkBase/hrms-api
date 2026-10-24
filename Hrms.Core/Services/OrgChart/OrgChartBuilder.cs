using Hrms.Domain.ValueObjects;

namespace Hrms.Core.Services.OrgChart;

public sealed record OrgDepartment(Guid Id, string Name, Guid? ParentId, Guid? HeadId);
public sealed record OrgSection(Guid Id, string Name, Guid? DepartmentId, Guid? HeadId);
public sealed record OrgPerson(Guid Id, string Name, string PositionName, int Level, Guid? ManagerId, Guid? DepartmentId, Guid? SectionId);

// Lays the company out as Company > Department (nested) > Section > people. A unit's head sits
// directly under the unit and carries everything else in it; people nest under their manager when
// the manager is in the same unit, otherwise they sit at the top of the unit ordered by position
// level. Everyone is placed once, so bad data (loops, a head named twice) can't duplicate or hang.
public static class OrgChartBuilder
{
    public const string UnassignedName = "Unassigned";

    public static List<OrgNodeModel> Build(
        string companyName,
        IReadOnlyCollection<OrgDepartment> departments,
        IReadOnlyCollection<OrgSection> sections,
        IReadOnlyCollection<OrgPerson> people)
    {
        var placement = new Placement(departments, sections, people);

        var children = placement.TopLevelDepartments().Select(placement.DepartmentNode).ToList();

        // Whoever is still unplaced: no department, or a department that no longer exists.
        var leftovers = people.Where(p => !placement.Placed.Contains(p.Id)).ToList();
        if (leftovers.Count > 0)
            children.Add(placement.Unit(Guid.Empty, OrgNodeKinds.Department, UnassignedName, null, () => [], leftovers));

        return
        [
            new OrgNodeModel
            {
                Id = "company",
                Kind = OrgNodeKinds.Company,
                Name = companyName,
                HeadCount = children.Sum(n => n.HeadCount),
                Children = children,
            },
        ];
    }

    // True if walking up from `parentId` arrives back at `id` (attaching would close a loop).
    public static bool ReachesSelf(Guid id, Guid parentId, IReadOnlyDictionary<Guid, Guid?> parents)
    {
        var seen = new HashSet<Guid>();
        Guid? current = parentId;
        while (current != null && seen.Add(current.Value))
        {
            if (current == id) return true;
            current = parents.GetValueOrDefault(current.Value);
        }
        return false;
    }

    private sealed class Placement
    {
        private readonly Dictionary<Guid, OrgPerson> _people;
        private readonly Dictionary<Guid, OrgDepartment> _departments;
        private readonly ILookup<Guid, OrgDepartment> _childDepartments;
        private readonly ILookup<Guid, OrgSection> _sectionsByDepartment;
        private readonly ILookup<Guid, OrgPerson> _peopleBySection;
        private readonly ILookup<Guid, OrgPerson> _peopleByDepartment;
        private readonly List<OrgDepartment> _topLevel;

        public HashSet<Guid> Placed { get; } = [];

        public Placement(IReadOnlyCollection<OrgDepartment> departments, IReadOnlyCollection<OrgSection> sections, IReadOnlyCollection<OrgPerson> people)
        {
            _people = people.ToDictionary(p => p.Id);
            _departments = departments.ToDictionary(d => d.Id);

            // A department nests under its parent unless the parent is missing or the chain loops.
            var parents = departments.ToDictionary(d => d.Id, d => d.ParentId);
            bool Nested(OrgDepartment d) => d.ParentId is { } parent && parent != d.Id
                && _departments.ContainsKey(parent) && !ReachesSelf(d.Id, parent, parents);
            _childDepartments = departments.Where(Nested).ToLookup(d => d.ParentId!.Value);
            _topLevel = departments.Where(d => !Nested(d)).OrderBy(d => d.Name).ToList();

            var usableSections = sections
                .Where(s => s.DepartmentId is { } id && _departments.ContainsKey(id))
                .ToDictionary(s => s.Id);
            _sectionsByDepartment = usableSections.Values.ToLookup(s => s.DepartmentId!.Value);

            // A person belongs to their section if it is usable, otherwise to their department.
            _peopleBySection = people
                .Where(p => p.SectionId is { } id && usableSections.ContainsKey(id))
                .ToLookup(p => p.SectionId!.Value);
            _peopleByDepartment = people
                .Where(p => !(p.SectionId is { } id && usableSections.ContainsKey(id))
                    && p.DepartmentId is { } dept && _departments.ContainsKey(dept))
                .ToLookup(p => p.DepartmentId!.Value);
        }

        public IEnumerable<OrgDepartment> TopLevelDepartments() => _topLevel;

        public OrgNodeModel DepartmentNode(OrgDepartment department) =>
            Unit(department.Id, OrgNodeKinds.Department, department.Name, department.HeadId,
                () => _childDepartments[department.Id].OrderBy(d => d.Name).Select(DepartmentNode)
                    .Concat(_sectionsByDepartment[department.Id].OrderBy(s => s.Name).Select(SectionNode))
                    .ToList(),
                _peopleByDepartment[department.Id].ToList());

        private OrgNodeModel SectionNode(OrgSection section) =>
            Unit(section.Id, OrgNodeKinds.Section, section.Name, section.HeadId, () => [], _peopleBySection[section.Id].ToList());

        // Builds one unit box. The head is claimed before the sub-units are built, so a department
        // head who also heads a section stays at the department; whoever reaches a person first keeps them.
        public OrgNodeModel Unit(Guid id, string kind, string name, Guid? headId, Func<List<OrgNodeModel>> buildSubUnits, List<OrgPerson> members)
        {
            var node = new OrgNodeModel { Id = id == Guid.Empty ? "unassigned" : id.ToString(), Kind = kind, Name = name };

            var head = headId is { } h && !Placed.Contains(h) && _people.TryGetValue(h, out var found) ? found : null;
            if (head != null) Placed.Add(head.Id);

            var subUnits = buildSubUnits();

            var open = members.Where(m => !Placed.Contains(m.Id)).ToList();
            foreach (var m in open) Placed.Add(m.Id);

            var body = NestByManager(open, head?.Id).Concat(subUnits).ToList();
            if (head == null)
            {
                node.Children = body;
            }
            else
            {
                var headNode = PersonNode(head);
                headNode.Children = body;
                headNode.HeadCount = 1 + body.Sum(n => n.HeadCount);
                node.Children = [headNode];
            }
            node.HeadCount = node.Children.Sum(n => n.HeadCount);
            return node;
        }

        // Managers inside the same unit own their reports; everyone else is a top of the unit.
        // Both are ordered by position level (highest first), then name.
        private static List<OrgNodeModel> NestByManager(List<OrgPerson> members, Guid? headId)
        {
            var nodes = members.ToDictionary(m => m.Id, PersonNode);
            var parents = members.ToDictionary(m => m.Id, m => m.ManagerId);
            var tops = new List<OrgPerson>();
            foreach (var m in members)
            {
                if (m.ManagerId is { } manager && manager != m.Id && manager != headId
                    && nodes.ContainsKey(manager) && !ReachesSelf(m.Id, manager, parents))
                    nodes[manager].Children.Add(nodes[m.Id]);
                else
                    tops.Add(m);
            }
            var roots = tops.OrderByDescending(t => t.Level).ThenBy(t => t.Name).Select(t => nodes[t.Id]).ToList();
            foreach (var root in roots) Finish(root);
            return roots;
        }

        // Orders each manager's reports and totals the people under them.
        private static void Finish(OrgNodeModel node)
        {
            node.Children = node.Children.OrderByDescending(c => c.Level).ThenBy(c => c.Name).ToList();
            foreach (var child in node.Children) Finish(child);
            node.HeadCount = 1 + node.Children.Sum(c => c.HeadCount);
        }

        private static OrgNodeModel PersonNode(OrgPerson p) => new()
        {
            Id = p.Id.ToString(),
            Kind = OrgNodeKinds.Employee,
            Name = p.Name,
            PositionName = p.PositionName,
            Level = p.Level,
            HeadCount = 1,
        };
    }
}
