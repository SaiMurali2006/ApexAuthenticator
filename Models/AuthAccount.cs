using System;

namespace ApexAuth.Models;

public sealed class AuthAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = "";
    public string Secret { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
