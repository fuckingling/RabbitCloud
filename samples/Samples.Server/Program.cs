﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rabbit.Cloud.Application;
using Rabbit.Extensions.Boot;
using Samples.Service;
using System.Threading.Tasks;

namespace Samples.Server
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            IHostedService ss;
            var hostBuilder = await RabbitBoot.BuildHostBuilderAsync(builder =>
            {
                builder
                .ConfigureHostConfiguration(b => b.AddJsonFile("appsettings.json"))
                .ConfigureServices(s =>
                {
                    s
                        .AddLogging()
                        .AddOptions()
                        .AddSingleton<ITestService, TestService>();
                })
                .UseRabbitApplicationConfigure();
            });

            await hostBuilder.RunConsoleAsync();
        }
    }
}