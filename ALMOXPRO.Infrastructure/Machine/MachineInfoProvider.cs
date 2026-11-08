using ALMOXPRO.Application.Interfaces;
using System.Net;
using System.Net.Sockets;

namespace ALMOXPRO.Infrastructure.Machine;

public class MachineInfoProvider : IMachineInfoProvider
{
    private readonly Lazy<string> _ipAddress = new(ResolveIpAddress);

    public string ComputerName => Environment.MachineName;

    public string IpAddress => _ipAddress.Value;

    private static string ResolveIpAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var ip = host.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            return ip?.ToString() ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }
}
