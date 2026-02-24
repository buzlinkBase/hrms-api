namespace Hrms.Domain.ValueObjects;

public class CreateAppGlobalSetting 
{
    public string SettingKey { get; set; } = string.Empty;
    public string Category    { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
    public string? MetaData { get; set; }
}
public class  UpdateAppGlobalSetting : CreateAppGlobalSetting
{
    public Guid Id { get; set; }
}

public class  AppGlobalSettingModel: UpdateAppGlobalSetting;