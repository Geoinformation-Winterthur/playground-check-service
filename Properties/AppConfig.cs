// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
using Npgsql;

namespace playground_check_service.Configuration
{
    public class AppConfig
    {
        public static IConfiguration Configuration;
        public static string wmsUrl;
        public static string connectionString;
        public static byte[] salt;

        static AppConfig()
        {
            if (AppConfig.Configuration == null)
            {
                var confBuilder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile($"appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables();
                AppConfig.Configuration = confBuilder.Build();

                AppConfig.wmsUrl = AppConfig.Configuration.GetValue<string>("WMS:ServiceUrl");

                string pgConnString = AppConfig.Configuration.GetValue<string>("Postgres:ConnectionString");
                NpgsqlConnectionStringBuilder pgStringBuilder =
                            new NpgsqlConnectionStringBuilder(pgConnString);
                AppConfig.connectionString = pgStringBuilder.ConnectionString;

                string saltBase64String = AppConfig.Configuration.GetValue<string>("SaltBase64String");
                AppConfig.salt = Convert.FromBase64String(saltBase64String);

            }
        }
    }
}
