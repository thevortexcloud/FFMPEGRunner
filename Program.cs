using System;
using CommandLine;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.DependencyInjection;

namespace Cake.FFMpegRunner {
    sealed class Program {
        private static Models.Configuration _config;

        static async Task Main(string[] args) {
            await ParseConfig(args);
            if (_config == null || !_config.IsValid) {
                return;
            }

            var services = new ServiceCollection();
            ConfigureService(services);


            using (ServiceProvider provider = services.BuildServiceProvider()) {
                var runner = provider.GetService<FFMpegRunner>();
                //await runner.BuildConverters();
                await runner.RunConverters();
            }
        }

        private static void ConfigureService(IServiceCollection services) {
            services.AddLogging(o => o.AddConsole());
            services.AddSingleton<Models.Configuration>(_config);
            services.AddSingleton<FFMpegRunner>();
        }

        private static async Task ParseConfig(string[] args) {
            var result = await Parser.Default.ParseArguments<Models.Configuration>(args)
                .WithParsedAsync<Models.Configuration>(o => {
                    if (o.InputDirectory != null && o.InputDirectory.Exists) {
                        Console.WriteLine(o.InputDirectory.FullName);
                    } else {
                        Console.WriteLine("Input directory not supplied or not found");
                    }

                    _config = o;
                    return Task.CompletedTask;
                });
        }
    }
}