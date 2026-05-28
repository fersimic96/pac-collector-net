using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PacCollector.Api.Services;

public sealed record NetInterface(string Name, string? Description, IReadOnlyList<string> Ipv4Addresses, bool IsUp);

public sealed class NetworkInfoService
{
    public IReadOnlyList<string> ListLocalIps()
    {
        var ips = new List<string>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    ips.Add(addr.Address.ToString());
                }
            }
        }
        catch { /* best-effort */ }
        return ips;
    }

    public IReadOnlyList<NetInterface> ListInterfaces()
    {
        var result = new List<NetInterface>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                var ipv4 = new List<string>();
                foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        ipv4.Add(addr.Address.ToString());
                }
                result.Add(new NetInterface(
                    Name: nic.Name,
                    Description: nic.Description,
                    Ipv4Addresses: ipv4,
                    IsUp: nic.OperationalStatus == OperationalStatus.Up));
            }
        }
        catch { /* best-effort */ }
        return result;
    }
}
