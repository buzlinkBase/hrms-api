using System.Net;
using System.Reflection;
using System.Security.Claims;
using Hrms.Api.Controllers;
using Hrms.Api.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;

namespace hrms.test.ApiTests;

/// <summary>
/// hrms-api's per-endpoint rate limits: one allowance per signed-in user, running out at the
/// limit, a readable 429, and which endpoints carry which limit.
/// </summary>
public class RateLimitPoliciesTests
{
    private static HttpContext Request(string ip, string? userId = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        if (userId != null)
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId)], "test"));
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static RateLimitRule Rule(string name) => RateLimitPolicies.Defaults.Single(r => r.Name == name);

    private static System.Threading.RateLimiting.RateLimiter LimiterFor(HttpContext context, RateLimitRule rule)
    {
        var partition = RateLimitPolicies.PartitionFor(context, rule);
        return partition.Factory(partition.PartitionKey);
    }

    [Fact]
    public void ColleaguesBehindOneOfficeIp_EachGetTheirOwnAllowance() =>
        RateLimitPolicies.PartitionKey(Request("203.0.113.5", "u-1"), RateLimitKey.User)
            .Should().NotBe(RateLimitPolicies.PartitionKey(Request("203.0.113.5", "u-2"), RateLimitKey.User));

    [Fact]
    public void WithoutASignedInUser_TheIpIsUsed() =>
        RateLimitPolicies.PartitionKey(Request("10.0.0.1"), RateLimitKey.User).Should().Be("ip:10.0.0.1");

    [Theory]
    [InlineData(RateLimitPolicies.HeavyGenerate)]
    [InlineData(RateLimitPolicies.FileUpload)]
    [InlineData(RateLimitPolicies.DocumentRender)]
    [InlineData(RateLimitPolicies.NotifySend)]
    public void EachPolicy_AllowsItsLimit_ThenRefuses(string name)
    {
        var rule = Rule(name);
        using var limiter = LimiterFor(Request("10.0.0.1", "u-1"), rule);

        for (var i = 0; i < rule.PermitLimit; i++)
            limiter.AttemptAcquire().IsAcquired.Should().BeTrue($"request {i + 1} of {rule.PermitLimit} is within the limit");
        limiter.AttemptAcquire().IsAcquired.Should().BeFalse();
    }

    [Fact]
    public void Limits_CanBeTunedPerEnvironment()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RateLimiting:document-render:PermitLimit"] = "100",
        }).Build();

        var rule = RateLimitPolicies.Configured(Rule(RateLimitPolicies.DocumentRender), configuration);

        rule.PermitLimit.Should().Be(100);
        rule.Window.Should().Be(TimeSpan.FromMinutes(1), "the window keeps its default");
    }

    [Fact]
    public async Task ARefusedRequest_Gets429_WithRetryAfter_AndAReadableMessage()
    {
        var context = Request("10.0.0.1", "u-1");
        using var limiter = LimiterFor(context, Rule(RateLimitPolicies.HeavyGenerate) with { PermitLimit = 1 });
        limiter.AttemptAcquire();
        using var refused = limiter.AttemptAcquire();

        await RateLimitPolicies.WriteRejectionAsync(context, refused, CancellationToken.None);

        context.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        int.Parse(context.Response.Headers.RetryAfter!).Should().BeGreaterThan(0);
        context.Response.Body.Position = 0;
        (await new StreamReader(context.Response.Body).ReadToEndAsync()).Should().Contain("Too many requests. Please wait");
    }

    public static TheoryData<Type, string, string> Endpoints => new()
    {
        { typeof(PayrollsController), nameof(PayrollsController.Generate), RateLimitPolicies.HeavyGenerate },
        { typeof(PayrollsController), nameof(PayrollsController.GenerateLastPay), RateLimitPolicies.HeavyGenerate },
        { typeof(PayrollsController), nameof(PayrollsController.PrintPayslip), RateLimitPolicies.DocumentRender },
        { typeof(AttendanceController), nameof(AttendanceController.Upload), RateLimitPolicies.FileUpload },
        { typeof(EmployeesController), nameof(EmployeesController.Upload), RateLimitPolicies.FileUpload },
        { typeof(EmployeesController), nameof(EmployeesController.Print201), RateLimitPolicies.DocumentRender },
        { typeof(MeController), nameof(MeController.PrintMyPayslip), RateLimitPolicies.DocumentRender },
        { typeof(AnnouncementsController), nameof(AnnouncementsController.Remind), RateLimitPolicies.NotifySend },
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public void Endpoint_CarriesItsLimit(Type controller, string action, string policy) =>
        controller.GetMethod(action)!.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName.Should().Be(policy);

    [Fact]
    public void ReadingManualAttendance_IsNotLimited() =>
        typeof(AttendanceController).GetMethod(nameof(AttendanceController.GetManualEntry))!
            .GetCustomAttribute<EnableRateLimitingAttribute>().Should().BeNull("it's a read named 'generate', not a generation");

    [Fact]
    public void EveryPolicyAnEndpointNames_IsRegistered()
    {
        var registered = RateLimitPolicies.Defaults.Select(r => r.Name).ToHashSet();
        var used = typeof(PayrollsController).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetMethods())
            .Select(m => m.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName)
            .OfType<string>()
            .ToList();

        used.Should().NotBeEmpty().And.OnlyContain(p => registered.Contains(p));
    }
}
