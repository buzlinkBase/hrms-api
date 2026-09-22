using System.Security.Claims;
using Hrms.Api.Controllers;
using Hrms.Domain;
using Hrms.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MockQueryable.NSubstitute;
using NSubstitute;

namespace hrms.test.ControllerTests;

/// <summary>
/// PayrollsController.ValidateBatchPermissionAsync -- the imperative permission check behind
/// PostBatch/DeleteBatch. Both endpoints act on a PayrollBatch shared across all 4 run types
/// (Regular/13th Month/Last Pay/Year-End Adjustment), so unlike every other action on this
/// controller, they can't be gated with a compile-time RequirePermissionAttribute code -- the
/// permission needed depends on the batch's actual PayrollType, resolved at runtime.
///
/// Tested directly against ValidateBatchPermissionAsync (internal, takes an explicit
/// ClaimsPrincipal), same approach as WorkSchedulePlansControllerTests.ValidateTeamScopeAsync.
/// </summary>
public class PayrollsControllerTests
{
    private static ClaimsPrincipal BuildUser(params string[] permissions)
    {
        var claims = permissions.Select(p => new Claim("permission", p));
        return new ClaimsPrincipal(new ClaimsIdentity(claims));
    }

    private static PayrollsController BuildController(params PayrollBatch[] batches)
    {
        var repo = Substitute.For<IRepository>();
        repo.FindOneAsync<PayrollBatch>(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => batches.FirstOrDefault(b => b.Id == call.Arg<Guid>()));

        var uow = Substitute.For<IUnitOfWorkService>();
        uow.Repository.Returns(repo);

        return new PayrollsController(
            service: null!,
            payrollService: null!,
            employeeService: null!,
            companyService: null!,
            payrollBatchService: new PayrollBatchService(uow),
            clientService: null!);
    }

    private static PayrollBatch BuildBatch(PayrollType type) => new() { Id = Guid.NewGuid(), PayrollType = type };

    [Theory]
    [InlineData(PayrollType.Regular, "Payroll Run")]
    [InlineData(PayrollType.ThirteenthMonth, "13th Month Run")]
    [InlineData(PayrollType.LastPay, "Last Pay Run")]
    [InlineData(PayrollType.YearEndAdjustment, "Year-End Adjustment Run")]
    public async Task ValidateBatchPermissionAsync_Allows_WhenCallerHasTheResolvedRunTypesPermission(PayrollType type, string feature)
    {
        var batch = BuildBatch(type);
        var controller = BuildController(batch);
        var user = BuildUser($"{feature}:Approve");

        var result = await controller.ValidateBatchPermissionAsync(user, batch.Id, "Approve", CancellationToken.None);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(PayrollType.Regular, "13th Month Run")]
    [InlineData(PayrollType.ThirteenthMonth, "Payroll Run")]
    public async Task ValidateBatchPermissionAsync_Forbids_WhenCallerHasAnUnrelatedRunTypesPermission(PayrollType type, string unrelatedFeature)
    {
        var batch = BuildBatch(type);
        var controller = BuildController(batch);
        var user = BuildUser($"{unrelatedFeature}:Approve");

        var result = await controller.ValidateBatchPermissionAsync(user, batch.Id, "Approve", CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task ValidateBatchPermissionAsync_Forbids_WhenCallerHasNoPermissionAtAll()
    {
        var batch = BuildBatch(PayrollType.Regular);
        var controller = BuildController(batch);
        var user = BuildUser();

        var result = await controller.ValidateBatchPermissionAsync(user, batch.Id, "Approve", CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task ValidateBatchPermissionAsync_ChecksCreate_ForDeleteBatchsAction()
    {
        // DeleteBatch passes "Create" (not "Approve") -- the closest fit given this catalog has
        // no Delete action; same permission that let the batch be generated is what's needed to
        // undo it.
        var batch = BuildBatch(PayrollType.LastPay);
        var controller = BuildController(batch);
        var approveOnly = BuildUser("Last Pay Run:Approve");
        var createOnly = BuildUser("Last Pay Run:Create");

        var approveResult = await controller.ValidateBatchPermissionAsync(approveOnly, batch.Id, "Create", CancellationToken.None);
        var createResult = await controller.ValidateBatchPermissionAsync(createOnly, batch.Id, "Create", CancellationToken.None);

        approveResult.Should().BeOfType<ForbidResult>();
        createResult.Should().BeNull();
    }

    [Fact]
    public async Task ValidateBatchPermissionAsync_NotFound_WhenBatchDoesNotExist()
    {
        var controller = BuildController();
        var user = BuildUser("Payroll Run:Approve");

        var result = await controller.ValidateBatchPermissionAsync(user, Guid.NewGuid(), "Approve", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteBatch_ReturnsSuccess_WhenTheBatchIsAlreadyGone()
    {
        // A second click, a stale list, or a concurrent delete can all reach DeleteBatch for a
        // batchId that no longer exists -- the caller's desired end state (this run doesn't
        // exist) already holds, so this must succeed silently rather than surface as an error
        // the way ValidateBatchPermissionAsync's own NotFoundResult otherwise would.
        var controller = BuildController();
        var user = BuildUser("Payroll Run:Create");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user },
        };

        var result = await controller.DeleteBatch(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }
}
