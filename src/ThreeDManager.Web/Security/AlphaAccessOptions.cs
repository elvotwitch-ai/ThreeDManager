namespace ThreeDManager.Web.Security;

public sealed class AlphaAccessOptions
{
    public const string SectionName = "AlphaAccess";

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
