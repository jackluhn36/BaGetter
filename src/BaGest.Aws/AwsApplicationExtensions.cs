using System;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.Credentials;
using Amazon.S3;
using BaGest.Aws;
using BaGest.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace BaGest;

public static class AwsApplicationExtensions
{
    public static BaGestApplication AddAwsS3Storage(this BaGestApplication app)
    {
        app.Services.AddBaGestOptions<S3StorageOptions>(nameof(BaGestOptions.Storage));

        app.Services.AddTransient<S3StorageService>();
        app.Services.TryAddTransient<IStorageService>(provider => provider.GetRequiredService<S3StorageService>());

        app.Services.AddProvider<IStorageService>((provider, config) =>
        {
            if (!config.HasStorageType("AwsS3")) return new NullStorageService();

            return provider.GetRequiredService<S3StorageService>();
        });

        app.Services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<S3StorageOptions>>().Value;

            var config = CreateS3Config(options);

            if (options.UseInstanceProfile)
            {
                var credentials = DefaultAWSCredentialsIdentityResolver.GetCredentials();
                return new AmazonS3Client(credentials, config);
            }

            if (!string.IsNullOrEmpty(options.AssumeRoleArn))
            {
                var credentials = DefaultAWSCredentialsIdentityResolver.GetCredentials();
                var assumedCredentials = AssumeRoleAsync(
                        credentials,
                        options.AssumeRoleArn,
                        $"BaGet-Session-{Guid.NewGuid()}")
                    .GetAwaiter()
                    .GetResult();

                return new AmazonS3Client(assumedCredentials, config);
            }

            if (!string.IsNullOrEmpty(options.AccessKey))
            {
                return new AmazonS3Client(
                    new BasicAWSCredentials(
                        options.AccessKey,
                        options.SecretKey),
                    config);
            }

            return new AmazonS3Client(config);
        });

        return app;
    }

    public static BaGestApplication AddAwsS3Storage(
        this BaGestApplication app,
        Action<S3StorageOptions> configure)
    {
        app.AddAwsS3Storage();
        app.Services.Configure(configure);
        return app;
    }

    internal static AmazonS3Config CreateS3Config(S3StorageOptions options)
    {
        var config = options.Endpoint != null
            ? new AmazonS3Config
            {
                ServiceURL = options.Endpoint.AbsoluteUri,
                ForcePathStyle = options.ForcePathStyle,
                // Used for request signing only; lets services that validate the region work.
                AuthenticationRegion = string.IsNullOrEmpty(options.Region) ? null : options.Region
            }
            : new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region),
                ForcePathStyle = options.ForcePathStyle
            };

        // Apply checksum calculation policy (AWSSDK 4 default is WHEN_SUPPORTED).
        // For S3-compatible services that reject optional checksums, set this to false.
        if (!options.RequestChecksumCalculation)
        {
            config.RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED;
        }

        return config;
    }

    private static async Task<AWSCredentials> AssumeRoleAsync(
        AWSCredentials credentials,
        string roleArn,
        string roleSessionName)
    {
        var assumedCredentials = new AssumeRoleAWSCredentials(credentials, roleArn, roleSessionName);
        var immutableCredentials = await credentials.GetCredentialsAsync();

        if (string.IsNullOrWhiteSpace(immutableCredentials.Token))
        {
            throw new InvalidOperationException($"Unable to assume role {roleArn}");
        }

        return assumedCredentials;
    }
}
