using System.ComponentModel.DataAnnotations;
using BaGest.Core;

namespace BaGest.Gcp;

public class GoogleCloudStorageOptions : StorageOptions
{
    [Required]
    public string BucketName { get; set; } = string.Empty;
}
