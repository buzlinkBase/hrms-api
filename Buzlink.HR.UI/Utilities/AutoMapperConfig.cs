using AutoMapper;

namespace Buzlink.HR.UI.Utilities;

public static class MapperConfig
{
    public static IMapper RegisterMappings()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<AutoMapperProfile>();
        });
        return config.CreateMapper();
    }
}
public static class MapperProvider
{
    public static IMapper Instance { get; } = MapperConfig.RegisterMappings();
}