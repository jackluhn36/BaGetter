using BaGest.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BaGest.Tencent;

public static class TencentApplicationExtensions
{
    public static BaGestApplication AddTencentOssStorage(this BaGestApplication app)
    {
        app.Services.AddBaGestOptions<TencentStorageOptions>(nameof(BaGestOptions.Storage));

        app.Services.AddTransient<TencentStorageService>();
        app.Services.TryAddTransient<IStorageService>(provider => provider.GetRequiredService<TencentStorageService>());

        app.Services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<TencentStorageOptions>>().Value;

            return new TencentCosClient(options);
        });

        app.Services.AddProvider<IStorageService>((provider, config) =>
        {
            if (!config.HasStorageType("TencentCos"))
                return new NullStorageService();

            return provider.GetRequiredService<TencentStorageService>();
        });

        return app;
    }

    public static BaGestApplication AddTencentOssStorage(
        this BaGestApplication app,
        Action<TencentStorageOptions> configure)
    {
        app.AddTencentOssStorage();
        app.Services.Configure(configure);
        return app;
    }
}
