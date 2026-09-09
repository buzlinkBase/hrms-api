using System.Linq.Expressions;
using Hrms.Domain.Entities;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace hrms.test.ServiceTests;

/// <summary>
/// RBAC Phase 1 — the engine only (seeding, role CRUD, permission assignment, user-role
/// assignment, effective-permission resolution). Nothing here enforces anything yet; these tests
/// exist to prove the primitives a future enforcement phase will build on actually behave
/// correctly (replace semantics, dedup, "no orphaned access").
/// </summary>
public class RbacServiceTests
{
    private static (IRepository Repo, IUnitOfWorkService Uow) BuildRepo(
        Permission[]? permissions = null,
        RolePermission[]? rolePermissions = null,
        UserRole[]? userRoles = null)
    {
        var perms = permissions ?? [];
        var rolePerms = rolePermissions ?? [];
        var userRolesList = userRoles ?? [];

        var repo = Substitute.For<IRepository>();
        repo.FindAll<Permission>().Returns(_ => perms.ToList().BuildMockDbSet());
        repo.Find<RolePermission>(Arg.Any<Expression<Func<RolePermission, bool>>>())
            .Returns(call => rolePerms.Where(call.Arg<Expression<Func<RolePermission, bool>>>().Compile()).ToList().BuildMockDbSet());
        repo.Find<UserRole>(Arg.Any<Expression<Func<UserRole, bool>>>())
            .Returns(call => userRolesList.Where(call.Arg<Expression<Func<UserRole, bool>>>().Compile()).ToList().BuildMockDbSet());

        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);
        uow.CommitChangesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        return (repo, uow);
    }

    [Fact]
    public async Task EnsureSeededAsync_SeedsCatalog_WhenEmpty()
    {
        var (repo, uow) = BuildRepo();
        var seeder = new PermissionCatalogSeederService(uow);

        await seeder.EnsureSeededAsync(CancellationToken.None);

        await repo.Received(1).AddRangeAsync(Arg.Is<IEnumerable<Permission>>(p => p.Any()), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureSeededAsync_DoesNothing_WhenAlreadySeeded()
    {
        var (repo, uow) = BuildRepo(permissions: [new Permission { Module = "Setup", Feature = "X", Action = "View", Code = "X:View" }]);
        var seeder = new PermissionCatalogSeederService(uow);

        await seeder.EnsureSeededAsync(CancellationToken.None);

        await repo.DidNotReceive().AddRangeAsync(Arg.Any<IEnumerable<Permission>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetPermissionsAsync_ReplacesExistingSetWithNewOne()
    {
        var roleId = Guid.NewGuid();
        var oldPermissionId = Guid.NewGuid();
        var newPermissionId = Guid.NewGuid();
        var (repo, uow) = BuildRepo(rolePermissions: [new RolePermission { RoleId = roleId, PermissionId = oldPermissionId }]);
        var service = new RoleService(uow);

        await service.SetPermissionsAsync(roleId, [newPermissionId], CancellationToken.None);

        repo.Received(1).RemoveRange(Arg.Is<IEnumerable<RolePermission>>(p => p.Any(x => x.PermissionId == oldPermissionId)));
        await repo.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<RolePermission>>(p => p.Count() == 1 && p.Single().PermissionId == newPermissionId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_ThrowsValidationException_WhenRoleStillAssignedToAUser()
    {
        var roleId = Guid.NewGuid();
        var (_, uow) = BuildRepo(userRoles: [new UserRole { RoleId = roleId, UserId = Guid.NewGuid() }]);
        var service = new RoleService(uow);

        var act = () => service.DeleteAsync(roleId, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task DeleteAsync_Succeeds_WhenRoleHasNoUserAssignments()
    {
        var roleId = Guid.NewGuid();
        var (repo, uow) = BuildRepo(rolePermissions: [new RolePermission { RoleId = roleId, PermissionId = Guid.NewGuid() }]);
        var service = new RoleService(uow);

        await service.DeleteAsync(roleId, CancellationToken.None);

        repo.Received(1).RemoveRange(Arg.Any<IEnumerable<RolePermission>>());
        repo.Received(1).Remove(Arg.Any<Expression<Func<Role, bool>>>());
    }

    [Fact]
    public async Task ReplaceRolesAsync_ReplacesExistingAssignmentsWithNewOnes()
    {
        var userId = Guid.NewGuid();
        var oldRoleId = Guid.NewGuid();
        var newRoleId = Guid.NewGuid();
        var (repo, uow) = BuildRepo(userRoles: [new UserRole { UserId = userId, RoleId = oldRoleId }]);
        var service = new UserRoleService(uow);

        await service.ReplaceRolesAsync(userId, [newRoleId], CancellationToken.None);

        repo.Received(1).RemoveRange(Arg.Is<IEnumerable<UserRole>>(r => r.Any(x => x.RoleId == oldRoleId)));
        await repo.Received(1).AddRangeAsync(
            Arg.Is<IEnumerable<UserRole>>(r => r.Count() == 1 && r.Single().RoleId == newRoleId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetEffectivePermissionCodesAsync_ReturnsDedupedUnionAcrossAllAssignedRoles()
    {
        var userId = Guid.NewGuid();
        var roleA = Guid.NewGuid();
        var roleB = Guid.NewGuid();
        var shared = new Permission { Id = Guid.NewGuid(), Code = "PayrollRun:View" };
        var onlyA = new Permission { Id = Guid.NewGuid(), Code = "PayrollRun:Approve" };
        var onlyB = new Permission { Id = Guid.NewGuid(), Code = "Users:Manage" };

        var (_, uow) = BuildRepo(
            userRoles: [new UserRole { UserId = userId, RoleId = roleA }, new UserRole { UserId = userId, RoleId = roleB }],
            rolePermissions:
            [
                new RolePermission { RoleId = roleA, PermissionId = shared.Id, Permission = shared },
                new RolePermission { RoleId = roleA, PermissionId = onlyA.Id, Permission = onlyA },
                new RolePermission { RoleId = roleB, PermissionId = shared.Id, Permission = shared },
                new RolePermission { RoleId = roleB, PermissionId = onlyB.Id, Permission = onlyB },
            ]);
        var service = new UserRoleService(uow);

        var result = await service.GetEffectivePermissionCodesAsync(userId, CancellationToken.None);

        result.Should().BeEquivalentTo(["PayrollRun:View", "PayrollRun:Approve", "Users:Manage"]);
    }

    [Fact]
    public async Task GetEffectivePermissionCodesAsync_ReturnsEmpty_WhenUserHasNoRoles()
    {
        var (_, uow) = BuildRepo();
        var service = new UserRoleService(uow);

        var result = await service.GetEffectivePermissionCodesAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
