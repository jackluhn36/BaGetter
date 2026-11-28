using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using BaGest.Core;

namespace BaGest.Aws;

public class S3StorageOptions : IValidatableObject
{
    [RequiredIf(nameof(SecretKey), null, IsInverted = true)]
    public string? AccessKey { get; set; }

    [RequiredIf(nameof(AccessKey), null, IsInverted = true)]
    public string? SecretKey { get; set; }

    [RequiredIf(nameof(Endpoint), null)]
    public string? Region { get; set; }

    [RequiredIf(nameof(Region), null)]
    public Uri? Endpoint { get; set; }

    /// <summary>
    /// Addresses the bucket in the URL path instead of the host name. Needed by S3-compatible
    /// services (such as MinIO) that do not support virtual-hosted-style addressing.
    /// </summary>
    public bool ForcePathStyle { get; set; }

    /// <summary>
    /// Whether uploads use chunked payload signing (aws-chunked). Turn this off for
    /// S3-compatible services that do not support it.
    /// </summary>
    public bool UseChunkEncoding { get; set; } = true;

    /// <summary>
    /// Whether the SDK should calculate request checksums whenever a service supports them.
    /// Set this to false for S3-compatible services (such as MinIO) that reject
    /// the default checksums introduced in AWSSDK 4. When false, checksums are
    /// only calculated when the operation strictly requires them.
    /// </summary>
    public bool RequestChecksumCalculation { get; set; } = true;

    [Required]
    public string Bucket { get; set; } = string.Empty;

    public string Prefix { get; set; } = string.Empty;

    public bool UseInstanceProfile { get; set; }

    public string? AssumeRoleArn { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Endpoint != null && !Endpoint.IsAbsoluteUri)
        {
            yield return new ValidationResult(
                $"The S3 {nameof(Endpoint)} must be an absolute URI.",
                new[] { nameof(Endpoint) });
        }
    }
}
