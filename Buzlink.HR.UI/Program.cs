
using AuthUI;
using BuzlinkRepository;
using Hrms.Infrastructure;
using Hrms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
namespace Buzlink.HR.UI
{
    internal static class Program
    {
        internal static MainMenu main;
        [STAThread]
        static void Main()
        {
            IServiceCollection services = new ServiceCollection();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            //DevExpress.LookAndFeel.UserLookAndFeel.Default.SetSkinStyle(Properties.Settings.Default.theme);
            //DevExpress.LookAndFeel.UserLookAndFeel.Default.SkinName = Properties.Settings.Default.theme;
            services.AuthServiceConfig();
            services.RegisterCoreServices();
            services.AddLogConfig();
            // we register this here since we have separate config for asp
            services.AddAutoMapper(typeof(MappingProfile));
            services.RegisterProviders();
            services.RegisterForms();
            services.AddDbContext<HrmsContext>((p, options) =>
            {
                var conServiceProvider = p.GetRequiredService<IDbConnectionProvider>();
                var tp = p.GetRequiredService<ITenantProvider>();
                var connString = conServiceProvider.GetConnectionString(tp.TenantId);
                options.UseMySql(connString, ServerVersion.AutoDetect(connString));
                options.AddInterceptors(new ApplyTenantInterceptor(tp), new SoftDeleteInterceptor());
                options.UseLazyLoadingProxies();
                options.ReplaceService<IModelCacheKeyFactory, TenantModelCacheKeyFactory>();
            });

            //services.AddDbContext<HrmsContext>((serviceProvider, optionsBuilder) =>
            //{
            //    var appConfig = serviceProvider.GetRequiredService<IAppConfigurationProvider>();
            //    var tenantAccessor = serviceProvider.GetRequiredService<ITenantContextAccessor>();
            //    var tenantProvider = serviceProvider.GetRequiredService<ITenantProvider>();
            //    var connMetadata = serviceProvider.GetRequiredService<IConnectionStringReaderProvider>();
            //    var defaultConn = appConfig.GetConnectionString("hrms");
            //    var tenantId = tenantAccessor.GetTenantId();
            //    var conServiceProvider = serviceProvider.GetRequiredService<IConnectionMetadataProvider>();
            //    var connString = conServiceProvider!.GetConnectionString(tenantId);
            //    optionsBuilder.UseMySql(connString, ServerVersion.AutoDetect(connString));
            //    optionsBuilder.UseLazyLoadingProxies();
            //});

            //services.RegisterMessageHandlers();
            var serviceProvider = services.BuildServiceProvider();
            BzServiceProvider.Instance.SetServiceProvider(serviceProvider);
            main = serviceProvider.GetRequiredService<MainMenu>();
            MainProgress.handler = main.UpdateProgress;
            Application.Run(main);
        }
    }
}
