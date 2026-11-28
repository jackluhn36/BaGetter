using System;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;

namespace BaGest.Core;

public class StringArrayToJsonConverter : ValueConverter<string[], string>
{
    public static readonly StringArrayToJsonConverter Instance = new StringArrayToJsonConverter();

    public StringArrayToJsonConverter()
        : base(
            v => JsonConvert.SerializeObject(v),
            v => !string.IsNullOrEmpty(v)
                ? JsonConvert.DeserializeObject<string[]>(v) ?? Array.Empty<string>()
                : Array.Empty<string>())
    {
    }
}
