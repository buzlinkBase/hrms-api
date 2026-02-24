namespace Hrms.Core.Services;

public interface ILogService
{
    void Log(IEntity model, string message);
}
public class LoggerService : ILogService
{
    public LoggerService()
    {
    }

    public void Log(IEntity model, string message)
    {

    }
}
