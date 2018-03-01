using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace ModuleLoader
{
    public class ModuleLoader
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ModuleLoader> _logger;

        public ModuleLoader(RequestDelegate next, ILoggerFactory logger)
        {
            _next = next;
            _logger = logger.CreateLogger<ModuleLoader>();
        }

        public async Task Invoke(HttpContext context)
        {
            await _next(context);
            _logger.LogInformation("Requested by ModuleLoader middleware: {0}", context.Request.Path);
            System.Console.WriteLine();
        }
    }

    public static partial class MiddleWareExtentions
    {
        public static IApplicationBuilder UseModuleLoader(this IApplicationBuilder builder)
        {
            return builder.Map("/module", HandleModuleLoad)
                            .Map("/fonts", HandleFontLoad);
        }

        private static void HandleFontLoad(IApplicationBuilder app)
        {
            var fontDirs = new string[] { "wwwroot/styles", "node_modules/font-awesome/fonts" };
            var mappedFonts = new Dictionary<string, string>();

            foreach (var dir in fontDirs)
            {
                foreach (var item in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
                {
                    mappedFonts.Add(Path.GetFileName(item), item.Replace("\\", "/"));
                }
            }

            app.Run(async context =>
            {
                var fileName = Path.GetFileName(context.Request.Path);
                if (mappedFonts.ContainsKey(fileName))
                {
                    await context.Response.SendFileAsync(mappedFonts[fileName]);
                }
            });
        }

        private static void HandleModuleLoad(IApplicationBuilder app)
        {
            var packagesMap = app.ApplicationServices.GetRequiredService<PackageMap>();
            var logger = app.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger("RAModuleLoader");

            app.Run(async context =>
            {
                var expectedPackage = context.Request.Path.Value.Replace("/", string.Empty).Replace(".js", string.Empty);
                logger.LogInformation("RAModuleLoader. Request module: {0}", expectedPackage);
                if (packagesMap.ContainsKey(expectedPackage))
                {
                    string package = string.Empty;
                    if (packagesMap.TryGetValue(expectedPackage, out package))
                    {
                        logger.LogInformation("RAModuleLoader. Found package: {0}", package);
                        var file = File.ReadAllText(package);

                        if (!file.StartsWith("define"))
                        {
                            file = "define(function (require, exports, module) {\n" + file + "\n});";
                            await GenerateStreamFromString(file).CopyToAsync(context.Response.Body);
                            return;
                        }
                        else
                        {
                            await context.Response.SendFileAsync(package);
                            return;
                        }
                    }
                    logger.LogInformation("RAModuleLoader. Package not found: {0}", expectedPackage);
                }
                else
                {
                    var fileName = $"wwwroot{context.Request.Path}";
                    logger.LogInformation("RAModuleLoader. Looking for: {0}", fileName);
                    await context.Response.SendFileAsync(fileName);
                }
            });
        }

        public static Stream GenerateStreamFromString(string s)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        private static Task<Stream> WrapPackage(string packageName)
        {
            var script = File.ReadAllText(packageName);
            if (!script.StartsWith("define"))
            {
                script = "define(function (require, exports, module) {" + script + "});";
            }

            return Task.FromResult(GenerateStreamFromString(script));
        }

        public static IServiceCollection AddModuleLoader(this IServiceCollection services)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var nodeModulesDir = Path.Combine(currentDirectory, "node_modules");
            var wwwroot = Path.Combine(currentDirectory, "wwwroot");
            var fileProvider = new PhysicalFileProvider(nodeModulesDir);
            var packagesMap = new PackageMap();

            var confBuilder = new ConfigurationBuilder();

            foreach (var item in Directory.GetDirectories(nodeModulesDir).Where(x => !x.Contains(".bin") && !x.Contains("@types")))
            {
                var packageName = item.Split(Path.DirectorySeparatorChar).Last();

                var mainFileLocation = JObject.Parse(File.ReadAllText(Path.Combine(nodeModulesDir, packageName, "package.json")))["main"];

                if (packageName.Contains("vue"))
                {
                    mainFileLocation = mainFileLocation.Value<string>().Replace(".common", "").Replace(".runtime", "");
                }

                if (packageName == "got")
                {
                    mainFileLocation = Path.Combine("wwwroot", "got.js");
                }

                if (mainFileLocation == null)
                {
                    mainFileLocation = Path.Combine(item, "index.js");

                    if (!File.Exists(mainFileLocation.Value<string>())) continue;
                }

                if (mainFileLocation.Value<string>().StartsWith("./"))
                    mainFileLocation = mainFileLocation.Value<string>().Replace("./", string.Empty);

                var mainFile = Path.Combine("node_modules", packageName, mainFileLocation.Value<string>()).Replace('\\', Path.AltDirectorySeparatorChar);

                if (!Path.HasExtension(mainFile))
                {
                    mainFile += ".js";
                }

                if (File.Exists(mainFile))
                {
                    packagesMap.Add(packageName, mainFile);
                }
            }

            services.AddSingleton<PackageMap>(packagesMap);

            return services;
        }
    }
}