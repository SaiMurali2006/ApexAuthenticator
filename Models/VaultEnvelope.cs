namespace ApexAuth.Models;

public sealed class VaultEnvelope
{
    public string App { get; set; } = "ApexAuth";
    public int Version { get; set; } = 1;
    public string Kdf { get; set; } = "scrypt";
    public string Salt { get; set; } = "";
    public int N { get; set; } = 131072;
    public int R { get; set; } = 8;
    public int P { get; set; } = 1;
    public string Token { get; set; } = "";
}
