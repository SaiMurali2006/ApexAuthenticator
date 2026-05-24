using System.Collections.Generic;

namespace ApexAuth.Models;

public sealed class VaultPayload
{
    public int Version { get; set; } = 1;
    public List<AuthAccount> Accounts { get; set; } = [];
}
