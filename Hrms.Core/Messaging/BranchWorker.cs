using Hrms.Domain.Entities;
using MassTransit;
namespace OnePunch.Auth.Core.Messaging;

public class TenantInfo
{
    public Guid Id { get; set; }
}
public class BranchWorker : IConsumer<BranchModel>
{
    private readonly BranchService _branchService;
    private readonly ITenantProvider _tenantProvider;

    public BranchWorker(BranchService branchService,
        TenantConnectionInfo connectionInfo,
        ITenantProvider tenantProvider)
    {
        _branchService = branchService;
        _tenantProvider = tenantProvider;
    }

    public async Task Consume(ConsumeContext<BranchModel> context)
    {
        var model = context.Message;
        var t = _tenantProvider;
        var branch = await _branchService.FindOneAsync(model.Id);
        if (branch == null)
        {
            branch = new Branch();
        }
        else
        {
            branch.Id = model.Id;
        }
        branch.Code = model.Code;
        branch.Name = model.Name;
        branch.ShortName = model.ShortName;
        branch.Address = model.Address;
        branch.Contact = model.Contact;
        branch.ManagerName = model.ManagerName;
        branch.TenantId = model.TenantId;
        branch.Status = model.Status;
        branch.DeletedAt = model.DeletedAt;
        await _branchService.AddOrUpdateAsync(branch);
        await _branchService.CommitChangesAsync(context.CancellationToken);

    }
}
