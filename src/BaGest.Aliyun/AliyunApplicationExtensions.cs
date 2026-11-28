using System;
using Aliyun.OSS;
using BaGest.Aliyun;
using BaGest.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BaGest;

public static class AliyunApplicationExtensions
{
    public static BaGestApplication AddAliyunOssStorage(this BaGestApplication app)
    {
        app.Services.AddBaGestOptions<AliyunStorageOptions>(nameof(BaGestOptions.Storage));

        app.Services.AddTransient<AliyunStorageService>();
        app.Services.TryAddTransient<IStorageService>(provider => provider.GetRequiredService<AliyunStorageService>());

        app.Services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<AliyunStorageOptions>>().Value;

            return new OssClient(options.Endpoint, options.AccessKey, options.AccessKeySecret);
        });

        app.Services.AddProvider<IStorageService>((provider, config) =>
        {
            if (!config.HasStorageType("AliyunOss")) return new NullStorageService();

            return provider.GetRequiredService<AliyunStorageService>();
        });

        return app;
    }

    public static BaGestApplication AddAliyunOssStorage(
        this BaGestApplication app,
        Action<AliyunStorageOptions> configure)
    {
        app.AddAliyunOssStorage();
        app.Services.Configure(configure);
        return app;
    }
}
