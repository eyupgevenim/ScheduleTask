using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scheduler.Abstraction;
using Scheduler.Infrastructure;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;

const string AppName = "Scheduler";

var Configuration = GetConfiguration();
Log.Logger = CreateSerilogLogger(Configuration);

try
{
    Log.Information("Configuring web host ({ApplicationContext})...", AppName);

    var host = Host
        .CreateDefaultBuilder(args)
        .ConfigureAppConfiguration(x => x.AddConfiguration(Configuration))
        .UseContentRoot(Directory.GetCurrentDirectory())
        .ConfigureLogging(x => x.AddConsole().SetMinimumLevel(LogLevel.Information))
        .ConfigureWebHostDefaults(webBuilder => 
        {
            webBuilder.ConfigureServices(services =>
            {
                services
                    .Configure<HostOptions>(option => { option.ShutdownTimeout = TimeSpan.FromSeconds(60); })
                    .Configure<Appsettings>(Configuration)
                    .Configure<ConnectionStringsOptions>(Configuration.GetSection(nameof(Appsettings.ConnectionStrings)))
                    .Configure<List<ScheduleTaskOptions>>(Configuration.GetSection(nameof(Appsettings.ScheduleTasks)))
                    .AddHangfire(configuration =>
                    {
                        //https://docs.hangfire.io/en/latest/configuration/using-sql-server.html
                        var connectionStringsOptions = Configuration.GetSection(nameof(Appsettings.ConnectionStrings)).Get<ConnectionStringsOptions>();
                        configuration
                        .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                        .UseSimpleAssemblyNameTypeSerializer()
                        .UseSqlServerStorage(connectionStringsOptions.ConnectionString, new SqlServerStorageOptions
                        {
                            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                            SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                            QueuePollInterval = TimeSpan.Zero,
                            UseRecommendedIsolationLevel = true,
                            UsePageLocksOnDequeue = true,
                            DisableGlobalLocks = true,
                            PrepareSchemaIfNecessary = true,
                            EnableHeavyMigrations = true
                        });
                    })
                    .AddHangfireServer()
                    .RegisterAllTypes<ITask>(new[] { typeof(ITask).Assembly }, lifetime: ServiceLifetime.Singleton)
                    .AddSingleton<ITaskManager, HangfireTaskManager>();
            })
            .Configure((hostingContext, app) =>
            {
                if (hostingContext.HostingEnvironment.IsDevelopment())
                {
                    app.UseDeveloperExceptionPage();
                }

                //app.UseRouting();
                app
                .Use(async (context, next) =>
                {
                    if (context.Request.Path.Value == "/")
                        context.Request.Path = "/ScheduleTask";
                    await next();
                })
                .UseHangfireDashboard("/ScheduleTask", options: new DashboardOptions { DashboardTitle = "Schedule Tasks", AppPath = null })
                .ApplicationServices.GetService<ITaskManager>().Initialize();

                //app.UseEndpoints(endpoints =>
                //{
                //    endpoints.MapHangfireDashboard();
                //});

            });
        })
        .Build();

    Log.Information("Starting web host ({ApplicationContext})...", AppName);

    host.CreateHangfireDatabase(services =>
    {
        var connectionStringsOptions = services.GetService<IOptions<ConnectionStringsOptions>>().Value;
        return connectionStringsOptions.ConnectionString;
    });

    await host.RunAsync();

    return 0;
}
catch(Exception ex)
{
    Log.Fatal(ex, "Program terminated unexpectedly ({ApplicationContext})!", AppName);
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

IConfiguration GetConfiguration() => new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddEnvironmentVariables()
        .Build();

Serilog.ILogger CreateSerilogLogger(IConfiguration configuration) => new LoggerConfiguration()
        .MinimumLevel.Verbose()
        .Enrich.WithProperty("ApplicationContext", AppName)
        .Enrich.FromLogContext()
        .WriteTo.File("Logs/Log_.txt", rollingInterval: RollingInterval.Day)
        .ReadFrom.Configuration(configuration)
        .CreateLogger();
