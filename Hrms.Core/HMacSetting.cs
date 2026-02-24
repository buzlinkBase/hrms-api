namespace Hrms.Api.Messaging;

public class HMacSetting
{
    public string SecretKey { get; set; }
    public string AppName { get; set; }
    public override string ToString()
    {
        return string.Concat(SecretKey, AppName);
    }
}

public class ApiKeySetting
{
    public string ApiKey { get; set; }
}


public class RabbitMQSettings
{
    public string Host { get; set; }
    public int Port { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string Exchange { get; set; }
    public string HrmsQue { get; set; }
}