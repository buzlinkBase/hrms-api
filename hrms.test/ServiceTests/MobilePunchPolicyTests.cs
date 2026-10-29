using DTR.Core;
using Hrms.Domain;
using Hrms.Domain.Entities;
using Hrms.Domain.ValueObjects;
using Hrms.Infrastructure;
using hrms.test.TestSupport;
using NetTopologySuite.Geometries;
using Onepunch.Common.Lib.Exceptions;

namespace hrms.test.ServiceTests;

/// <summary>
/// Which way a branch / project site lets employees punch from the mobile app: open (like My Portal),
/// geofence, QR, NFC, or geofence plus a tag. A site's own mode overrides its branch's; a site left on
/// "follow branch" uses the branch's.
/// </summary>
public class MobilePunchPolicyTests
{
    // A 0.01-degree square around (lat 14.55, lng 121.05); GeoJSON order is (lng, lat).
    private static Polygon Square() => new(new LinearRing(
    [
        new Coordinate(121.04, 14.54),
        new Coordinate(121.06, 14.54),
        new Coordinate(121.06, 14.56),
        new Coordinate(121.04, 14.56),
        new Coordinate(121.04, 14.54),
    ]));

    private static readonly MobilePunchEvidence Inside = new(14.55, 121.05, null, null);
    private static readonly MobilePunchEvidence Outside = new(14.70, 121.30, null, null);
    private static readonly MobilePunchEvidence Nothing = new(null, null, null, null);

    private static ResolvedPunchMode Rule(MobilePunchMode mode, Polygon? boundary = null) =>
        new(mode, PunchLocationKind.Site, Guid.Parse("11111111-1111-1111-1111-111111111111"), boundary);

    [Fact]
    public void Open_AllowsAPunchWithNoLocationAndNoTag()
    {
        MobilePunchPolicy.Check(Rule(MobilePunchMode.OPEN), Nothing).Should().BeNull();
    }

    [Fact]
    public void Geofence_AllowsInsideAndBlocksOutside()
    {
        var rule = Rule(MobilePunchMode.GEOFENCE, Square());

        MobilePunchPolicy.Check(rule, Inside).Should().BeNull();
        MobilePunchPolicy.Check(rule, Outside).Should().Contain("outside");
    }

    [Fact]
    public void Geofence_NeedsTheEmployeesLocation()
    {
        MobilePunchPolicy.Check(Rule(MobilePunchMode.GEOFENCE, Square()), Nothing).Should().Contain("location");
    }

    [Fact]
    public void Geofence_WithNoBoundaryDrawn_BlocksInsteadOfOpeningUp()
    {
        MobilePunchPolicy.Check(Rule(MobilePunchMode.GEOFENCE), Inside).Should().Contain("no boundary");
    }

    [Fact]
    public void Qr_NeedsTheLocationsOwnCode()
    {
        var rule = Rule(MobilePunchMode.QR);

        MobilePunchPolicy.Check(rule, Nothing with { QrCode = rule.ExpectedTag }).Should().BeNull();
        MobilePunchPolicy.Check(rule, Nothing with { QrCode = rule.ExpectedTag.ToUpperInvariant() }).Should().BeNull();
        MobilePunchPolicy.Check(rule, Nothing).Should().Contain("QR");
        MobilePunchPolicy.Check(rule, Nothing with { QrCode = "onepunch:site:" + Guid.NewGuid() }).Should().Contain("QR");
    }

    [Fact]
    public void Qr_DoesNotAcceptTheNfcTagInItsPlace()
    {
        var rule = Rule(MobilePunchMode.QR);

        MobilePunchPolicy.Check(rule, Nothing with { NfcTag = rule.ExpectedTag }).Should().Contain("QR");
    }

    [Fact]
    public void Nfc_NeedsTheLocationsOwnTag()
    {
        var rule = Rule(MobilePunchMode.NFC);

        MobilePunchPolicy.Check(rule, Nothing with { NfcTag = rule.ExpectedTag }).Should().BeNull();
        MobilePunchPolicy.Check(rule, Nothing).Should().Contain("NFC");
    }

    [Theory]
    [InlineData(MobilePunchMode.GEOFENCE_QR)]
    [InlineData(MobilePunchMode.GEOFENCE_NFC)]
    public void Combined_NeedsBothTheLocationAndTheTag(MobilePunchMode mode)
    {
        var rule = Rule(mode, Square());
        var tag = rule.ExpectedTag;
        var withTag = mode == MobilePunchMode.GEOFENCE_QR ? Nothing with { QrCode = tag } : Nothing with { NfcTag = tag };

        MobilePunchPolicy.Check(rule, Inside with { QrCode = withTag.QrCode, NfcTag = withTag.NfcTag }).Should().BeNull();
        MobilePunchPolicy.Check(rule, Inside).Should().NotBeNull();
        MobilePunchPolicy.Check(rule, Outside with { QrCode = withTag.QrCode, NfcTag = withTag.NfcTag }).Should().Contain("outside");
    }

    [Fact]
    public void Resolve_SiteOwnModeWinsOverTheBranch()
    {
        var branch = new Branch { Id = Guid.NewGuid(), PunchMode = MobilePunchMode.GEOFENCE, Boundary = Square() };
        var site = new CostCenters { Id = Guid.NewGuid(), PunchMode = MobilePunchMode.QR };

        var rule = MobilePunchPolicy.Resolve(branch, site);

        rule.Mode.Should().Be(MobilePunchMode.QR);
        rule.Kind.Should().Be(PunchLocationKind.Site);
        rule.LocationId.Should().Be(site.Id);
    }

    [Fact]
    public void Resolve_SiteSetToOpen_OverridesAStrictBranch()
    {
        var branch = new Branch { Id = Guid.NewGuid(), PunchMode = MobilePunchMode.GEOFENCE };
        var site = new CostCenters { Id = Guid.NewGuid(), PunchMode = MobilePunchMode.OPEN };

        MobilePunchPolicy.Resolve(branch, site).Mode.Should().Be(MobilePunchMode.OPEN);
    }

    [Fact]
    public void Resolve_SiteFollowingBranch_UsesTheBranchModeAndBoundary()
    {
        var boundary = Square();
        var branch = new Branch { Id = Guid.NewGuid(), PunchMode = MobilePunchMode.GEOFENCE_NFC, Boundary = boundary };
        var site = new CostCenters { Id = Guid.NewGuid(), PunchMode = null };

        var rule = MobilePunchPolicy.Resolve(branch, site);

        rule.Mode.Should().Be(MobilePunchMode.GEOFENCE_NFC);
        rule.Kind.Should().Be(PunchLocationKind.Branch);
        rule.LocationId.Should().Be(branch.Id);
        rule.Boundary.Should().BeSameAs(boundary);
    }

    [Fact]
    public void Resolve_NoBranchAndNoSite_IsOpen()
    {
        MobilePunchPolicy.Resolve(null, null).Mode.Should().Be(MobilePunchMode.OPEN);
    }

    private static MobilePunchPolicyService ServiceFor(HrmsContext context)
    {
        var uow = new UnitOfWorkService(context);
        return new MobilePunchPolicyService(uow, new GeneralSettingService(uow));
    }

    // The company switch; off (no row) unless a test turns it on.
    private static GeneralSetting Switch(bool on) => new()
    {
        IdentityType = PayrollSettingsIdentity.IdentityType,
        Description = PayrollSettingsIdentity.KeyMobilePunchModesEnabled,
        Value = on.ToString(),
    };

    [Fact]
    public async Task Service_WhenTheCompanySwitchIsOff_EveryLocationIsOpen()
    {
        using var db = new SqliteHrmsContext();
        var branch = new Branch { Id = Guid.NewGuid(), Code = "B1", Name = "Manila", PunchMode = MobilePunchMode.GEOFENCE_QR };
        await using (var seed = db.NewContext())
        {
            seed.Branches.Add(branch);
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = ServiceFor(context);

        (await service.ResolveAsync(branch.Id, null, CancellationToken.None)).Mode.Should().Be(MobilePunchMode.OPEN);
        var allowed = () => service.EnsureAllowedAsync(branch.Id, null, Nothing, CancellationToken.None);
        await allowed.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Service_SiteWithoutOwnMode_FollowsItsBranchEvenWhenNoBranchIsGiven()
    {
        using var db = new SqliteHrmsContext();
        var branch = new Branch { Id = Guid.NewGuid(), Code = "B1", Name = "Manila", PunchMode = MobilePunchMode.QR };
        var site = new CostCenters { Id = Guid.NewGuid(), Code = "S1", Name = "Tower", BranchId = branch.Id };
        await using (var seed = db.NewContext())
        {
            seed.GeneralSettings.Add(Switch(true));
            seed.Branches.Add(branch);
            seed.Areas.Add(site);
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var rule = await ServiceFor(context).ResolveAsync(null, site.Id, CancellationToken.None);

        rule.Mode.Should().Be(MobilePunchMode.QR);
        rule.LocationId.Should().Be(branch.Id);
    }

    [Fact]
    public async Task Service_EnsureAllowed_ThrowsTheReasonWhenTheTagIsMissing()
    {
        using var db = new SqliteHrmsContext();
        var branch = new Branch { Id = Guid.NewGuid(), Code = "B1", Name = "Manila", PunchMode = MobilePunchMode.NFC };
        await using (var seed = db.NewContext())
        {
            seed.GeneralSettings.Add(Switch(true));
            seed.Branches.Add(branch);
            await seed.SaveChangesAsync();
        }

        await using var context = db.NewContext();
        var service = ServiceFor(context);

        var blocked = () => service.EnsureAllowedAsync(branch.Id, null, Nothing, CancellationToken.None);
        await blocked.Should().ThrowAsync<GuardException>().WithMessage("*NFC*");

        var tag = MobilePunchPolicy.TagFor(PunchLocationKind.Branch, branch.Id);
        var allowed = () => service.EnsureAllowedAsync(branch.Id, null, Nothing with { NfcTag = tag }, CancellationToken.None);
        await allowed.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Service_EmployeeWithNoBranchOrSite_IsOpen()
    {
        using var db = new SqliteHrmsContext();
        await using var context = db.NewContext();

        var rule = await ServiceFor(context).ResolveAsync(null, null, CancellationToken.None);

        rule.Mode.Should().Be(MobilePunchMode.OPEN);
    }
}
