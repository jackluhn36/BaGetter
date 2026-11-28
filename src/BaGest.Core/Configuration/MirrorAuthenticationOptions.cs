using System.Collections.Generic;

namespace BaGest.Core;

public class MirrorAuthenticationOptions
{
    public MirrorAuthenticationType Type { get; set; } = MirrorAuthenticationType.None;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public Dictionary<string, string> CustomHeaders { get; set; } = [];
}
