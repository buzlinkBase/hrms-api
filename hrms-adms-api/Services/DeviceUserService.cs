namespace Hrms.adms.Services;

public class DeviceUserService : BaseService<DeviceUser>
{
    public DeviceUserService(IUnitOfWorkService uow) : base(uow) { }

    public async Task UpsertAsync(List<DeviceUser> users, CancellationToken token)
    {
        foreach (var user in users)
        {
            var existing = await Context.DeviceUsers
                .FirstOrDefaultAsync(x => x.TenantId == user.TenantId &&
                                          x.SN == user.SN &&
                                          x.UserPin == user.UserPin, token);
            if (existing != null)
            {
                existing.Name = user.Name;
                existing.Priority = user.Priority;
                existing.Password = user.Password;
                existing.Card = user.Card;
            }
            else
            {
                Context.DeviceUsers.Add(user);
            }
        }
        await Context.SaveChangesAsync(token);
    }
}
