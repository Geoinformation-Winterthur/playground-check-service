// <copyright company="Vermessungsamt Winterthur">
//      Author: Edgar Butwilowski
//      Copyright (c) Vermessungsamt Winterthur. All rights reserved.
// </copyright>
using playground_check_service.Configuration;
using Serilog;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Prometheus;

Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(AppConfig.Configuration)
            .CreateLogger();

try
{

    Log.Information("Starting playground service.");

    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    // Add services to the container.

    string serviceUrl = AppConfig.Configuration.GetValue<string>("URL:ServiceUrl");
    string securityKey = AppConfig.Configuration.GetValue<string>("SecurityKey");


    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
        .AddJwtBearer(opts =>
        {
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = serviceUrl,
                ValidAudience = serviceUrl,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(securityKey))
            };
        });

    builder.Services.AddControllers();
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    string clientUrl = AppConfig.Configuration.GetValue<string>("URL:ClientUrl");
    string policyName = "AllowCorsOrigins";


    List<string> allowedOrigins = new List<string>();
    allowedOrigins.Add(clientUrl);

    builder.Services.AddCors(opt =>
    {
        opt.AddPolicy(policyName, policy =>
            policy.WithOrigins(allowedOrigins.ToArray())
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials());
    });

    /*
    builder.Services.AddAuthorization(options => 
    {
        options.AddPolicy("BasicAuthenticationForPrometheus", policy => {
            policy.Requirements.Add(...); TODO
        });
    });
    */

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseSwagger();
    app.UseSwaggerUI();

    // app.UseHttpsRedirection();

    app.UseRouting();

    app.UseCors(policyName);

    app.UseAuthentication();

    app.UseAuthorization();

    app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            // endpoints.MapMetrics().RequireAuthorization("BasicAuthenticationForPrometheus");
        });

    app.Run();

    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Playground service stopped with error.");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}