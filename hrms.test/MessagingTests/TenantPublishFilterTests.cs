using Hrms.Core.Messaging.Filter;
using MassTransit;
using NSubstitute;
using Onepunch.Common.Lib.DTO;

namespace hrms.test.MessagingTests;

/// <summary>
/// The company header must survive a re-run of the publish pipeline outside the request that
/// published the message (outbox delivery, background scope) -- there the tenant provider is
/// empty, and overwriting the original header with Guid.Empty cut approval pushes off from the
/// recipient's company + email group.
/// </summary>
public class TenantPublishFilterTests
{
    private static async Task<SendHeaders> RunAsync(Guid providerTenant, string? existingHeader)
    {
        var tenantProvider = Substitute.For<ITenantProvider>();
        tenantProvider.TenantId.Returns(providerTenant);

        var headers = Substitute.For<SendHeaders>();
        headers.TryGetHeader(TenantPublishFilter<ApprovalNotificationRequested>.HeaderName, out Arg.Any<object?>())
            .Returns(call =>
            {
                call[1] = existingHeader;
                return existingHeader != null;
            });

        var context = Substitute.For<PublishContext<ApprovalNotificationRequested>>();
        context.Headers.Returns(headers);
        var next = Substitute.For<IPipe<PublishContext<ApprovalNotificationRequested>>>();

        await new TenantPublishFilter<ApprovalNotificationRequested>(tenantProvider).Send(context, next);

        await next.Received(1).Send(context);
        return headers;
    }

    [Fact]
    public async Task StampsTheCurrentCompany_WhenTheMessageHasNone()
    {
        var tenantId = Guid.NewGuid();

        var headers = await RunAsync(tenantId, existingHeader: null);

        headers.Received(1).Set(TenantPublishFilter<ApprovalNotificationRequested>.HeaderName, tenantId.ToString());
    }

    [Fact]
    public async Task KeepsTheOriginalCompany_WhenRerunWithoutOne()
    {
        var original = Guid.NewGuid().ToString();

        var headers = await RunAsync(Guid.Empty, existingHeader: original);

        headers.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ReplacesAnEmptyHeader_WithTheCurrentCompany()
    {
        var tenantId = Guid.NewGuid();

        var headers = await RunAsync(tenantId, existingHeader: Guid.Empty.ToString());

        headers.Received(1).Set(TenantPublishFilter<ApprovalNotificationRequested>.HeaderName, tenantId.ToString());
    }

    [Fact]
    public async Task StillStampsAnEmptyCompany_WhenNeitherSideHasOne()
    {
        // TenantConsumeFilter rejects a message with no header at all.
        var headers = await RunAsync(Guid.Empty, existingHeader: null);

        headers.Received(1).Set(TenantPublishFilter<ApprovalNotificationRequested>.HeaderName, Guid.Empty.ToString());
    }
}
