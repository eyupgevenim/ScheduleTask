namespace Scheduler.Infrastructure
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Polly;
    using System;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Reflection;

    public static class Extensions
    {
        public static IServiceCollection RegisterAllTypes<T>(this IServiceCollection services, Assembly[] assemblies, ServiceLifetime lifetime = ServiceLifetime.Transient)
        {
            foreach (var type in assemblies.SelectMany(a => a.DefinedTypes.Where(x => x.GetInterfaces().Contains(typeof(T)))))
            {
                services.Add(new ServiceDescriptor(typeof(T), type, lifetime));
            }

            return services;
        }

        public static IHost CreateHangfireDatabase(this IHost host, Func<IServiceProvider, string> getConnectionString)
        {
            using (var scope = host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var logger = services.GetRequiredService<ILogger<ConnectionStringsOptions>>();

                try
                {
                    logger.LogInformation("Getting started creating the Hangfire database");

                    var retries = 10;
                    var retry = Policy.Handle<SqlException>()
                        .WaitAndRetry(
                            retryCount: retries,
                            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                            onRetry: (exception, timeSpan, retry, ctx) =>
                            {
                                logger.LogWarning(exception, "Exception {ExceptionType} with message {Message} detected on attempt {retry} of {retries}", 
                                    exception.GetType().Name, exception.Message, retry, retries);
                            });

                    retry.Execute(() => 
                    {
                        var hangfireConnectionString = getConnectionString(services);
                        var databaseNameWithKey = hangfireConnectionString.Split(";").FirstOrDefault(x => x.StartsWith("Database=", StringComparison.OrdinalIgnoreCase));

                        var databaseName = databaseNameWithKey.Split("=").LastOrDefault();
                        var query = $"IF NOT EXISTS(SELECT 1 FROM sys.databases WHERE name=N'{databaseName}') BEGIN CREATE DATABASE [{databaseName}] END;";

                        var masterConnectionString = hangfireConnectionString.Replace(databaseNameWithKey, "Database=master");

                        ExecuteSqlCommand(masterConnectionString, query);
                    });

                    logger.LogInformation("Finish creating the Hangfire database");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while creating the Hangfire database");
                }
            }

            return host;

            int ExecuteSqlCommand(string connectionString, string queryString)
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                using (SqlCommand command = new SqlCommand(queryString, connection))
                {
                    connection.Open();
                    return command.ExecuteNonQuery();
                }
            }
        }

    }
}
