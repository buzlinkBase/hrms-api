namespace Hrms.Domain.Entities
{
    public class AppGlobalSetting : BaseEntity
    {
        public string SettingKey { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string SettingValue { get; set; } = string.Empty;
        public string? MetaData { get; set; }
    }
}
