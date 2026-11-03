namespace SaEkg.Infrastructure.Auth;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public string[] ExternalAccessAllowedRoles { get; set; } = ["Patient"];
    public string[] InternalNetworks { get; set; } = ["127.0.0.0/8", "::1/128", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16"];
    public bool EnforceInternalNetworkCheck { get; set; }
}
