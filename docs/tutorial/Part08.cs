using Microsoft.Extensions.Hosting;
using static System.Console;

namespace Tutorial;

public static class Part08{
    // Host GetHost(string tenantId, string userIdOrIP) => Hoststs
    // .Select(host => (
    //     Host: host, 
    //     Weight: Hash(host.Id, tenantId)
    // ))
    // .OrderBy(p.Weight)
    // .Select(p => p.Host)
    // .Skip(Hash(userIdOrIP) % K)
    // .First();
}

