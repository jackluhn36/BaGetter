using System;
using BaGest.Core;
using BaGest.Gcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BaGest;

public static class GoogleCloudApplicationExtensions
{
    public static BaGestApplication AddGoogleCloudStorage(this BaGestApplication app)
    {
        app.Services.AddBaGestOptions<GoogleCloudStorageOptions>(nameof(BaGestOptions.Storage));
        app.Services.AddTransient<GoogleCloudStorageService>();

        app.Services.TryAddTransient<IStorageService>(provider => provider.GetRequiredService<GoogleCloudStorageService>());

        app.Services.AddProvider<IStorageService>((provider, config) =>
        {
            if (!config.HasStorageType("GoogleCloud")) return new NullStorageService();

            return provider.GetRequiredService<GoogleCloudStorageService>();
        });

        return app;
    }

    public static BaGestApplication AddGoogleCloudStorage(
        this BaGestApplication app,
        Action<GoogleCloudStorageOptions> configure)
    {
        app.AddGoogleCloudStorage();
        app.Services.Configure(configure);
        return app;
    }
}
