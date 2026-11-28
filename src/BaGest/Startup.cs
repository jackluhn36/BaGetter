using System;
using System.Globalization;
using System.Linq;
using BaGest.Authentication;
using BaGest.Core;
using BaGest.Core.Extensions;
using BaGest.Tencent;
using BaGest.Web;
using BaGest.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using HealthCheckOptions = Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions;

namespace BaGest;

public class Startup
{
    private IConfiguration Configuration { get; }

    public Startup(IConfiguration configuration)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.ConfigureOptions<ValidateBaGestOptions>();
        services.ConfigureOptions<ConfigureBaGestServer>();

        services.AddBaGestOptions<IISServerOptions>(nameof(IISServerOptions));
        services.AddBaGestWebApplication(ConfigureBaGestApplication);

        // You can swap between implementations of subsystems like storage and search using BaGest's configuration.
        // Each subsystem's implementation has a provider that reads the configuration to determine if it should be
        // activated. BaGest will run through all its providers until it finds one that is active.
        services.AddScoped<IContext>(services => DependencyInjectionExtensions.GetServiceFromProviders<IContext>(services)!);
        services.AddTransient<IStorageService>(services => DependencyInjectionExtensions.GetServiceFromProviders<IStorageService>(services)!);
        services.AddTransient<IPackageDatabase>(services => DependencyInjectionExtensions.GetServiceFromProviders<IPackageDatabase>(services)!);
        services.AddTransient<ISearchService>(services => DependencyInjectionExtensions.GetServiceFromProviders<ISearchService>(services)!);
        services.AddTransient<ISearchIndexer>(services => DependencyInjectionExtensions.GetServiceFromProviders<ISearchIndexer>(services)!);

        services.AddHealthChecks();

        services.AddCors();
    }

    private void ConfigureBaGestApplication(BaGestApplication app)
    {
        //Add base authentication and authorization
        app.AddNugetBasicHttpAuthentication();
        app.AddNugetBasicHttpAuthorization();

        // Add database providers.
        app.AddAzureTableDatabase();
        app.AddMySqlDatabase();
        app.AddPostgreSqlDatabase();
        app.AddSqliteDatabase();
        app.AddSqlServerDatabase();

        // Add storage providers.
        app.AddFileStorage();
        app.AddAliyunOssStorage();
        app.AddAwsS3Storage();
        app.AddAzureBlobStorage();
        app.AddGoogleCloudStorage();
        app.AddTencentOssStorage();

        // Add search providers.
        //app.AddAzureSearch();
    }

    // Dates and numbers are formatted using the culture sent by the browser (Accept-Language).
    // The UI language itself stays unchanged, and requests without a supported culture
    // keep using the server's default culture.
    private static RequestLocalizationOptions CreateRequestLocalizationOptions()
    {
        var options = new RequestLocalizationOptions
        {
            SupportedCultures = CultureInfo.GetCultures(CultureTypes.AllCultures)
                .Where(c => !string.IsNullOrEmpty(c.Name))
                .ToList(),
            SupportedUICultures = new[] { CultureInfo.CurrentUICulture },
        };

        options.RequestCultureProviders.Clear();
        options.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());

        return options;
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        var options = Configuration.Get<BaGestOptions>()
            ?? throw new InvalidOperationException("BaGest configuration could not be loaded.");

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseStatusCodePages();
        }

        app.UseForwardedHeaders();
        app.UsePathBase(options.PathBase);

        app.UseStaticFiles();
        app.UseRequestLocalization(CreateRequestLocalizationOptions());
        app.UseAuthentication();
        app.UseRouting();
        app.UseAuthorization();

        app.UseCors(ConfigureBaGestServer.CorsPolicy);

        app.UseOperationCancelledMiddleware();

        app.UseEndpoints(endpoints =>
        {
            var baget = new BaGestEndpointBuilder();

            baget.MapEndpoints(endpoints);
        });

        var healthCheck = options.HealthCheck
            ?? throw new InvalidOperationException("BaGest health check configuration could not be loaded.");

        app.UseHealthChecks(healthCheck.Path,
            new HealthCheckOptions
            {
                ResponseWriter = async (context, report) =>
                {
                    await report.FormatAsJson(context.Response.Body, options.Statistics?.ListConfiguredServices ?? false, healthCheck.StatusPropertyName,
                        context.RequestAborted);
                },
                Predicate = check => check.IsConfigured(options)
            }
        );
    }
}
