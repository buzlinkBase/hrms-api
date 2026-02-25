using AutoMapper;

namespace Hrms.Api;
//public static class AutoMapperConfig
//{
//    public static IMapper Initialize()
//    {
//        var config = new MapperConfiguration(cfg =>
//        {
//            cfg.SourceMemberNamingConvention = new PascalCaseNamingConvention();
//            cfg.DestinationMemberNamingConvention = new LowerUnderscoreNamingConvention();
//        });
//        config.AssertConfigurationIsValid();
//        return config.CreateMapper();
//    }
//}
public class AspAutoMapperProfile : Profile
{
    public AspAutoMapperProfile()
    {
    }
}
