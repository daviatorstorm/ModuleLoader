using Microsoft.Extensions.DependencyInjection;
namespace ModuleLoader
{
    public static partial class MiddleWareExtentions
    {
        public static IServiceCollection AddRAScriptBundler(this IServiceCollection services, string[] paths)
        {
            services.AddSingleton(new BundleService(paths));

            return services;
        }
    }
}
