using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Attendance.Api.Services;

/// <summary>
/// Detects this machine's LAN IPv4 address so the QR deep link
/// (<c>Kiosk:ScanBaseUrl = "auto"</c>) always points at whatever address the
/// router assigned — surviving DHCP reassignments and network changes without
/// any config edits.
/// </summary>
public static class NetworkHelper
{
    /// <summary>
    /// Returns the machine's outbound LAN IPv4, or <see langword="null"/>
    /// when none can be determined (no network at all).
    /// </summary>
    public static string? DetectLanIpv4()
    {
        // Preferred: a UDP "connect" makes the OS pick the outbound
        // interface/address via its routing table without sending a single
        // packet — works even when the network has no internet access, as
        // long as a default route exists (e.g. a phone hotspot).
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint endpoint
                && !IPAddress.IsLoopback(endpoint.Address))
            {
                return endpoint.Address.ToString();
            }
        }
        catch
        {
            // No default route — fall through to interface enumeration.
        }

        // Fallback: first operational, non-loopback interface that has both
        // an IPv4 address and a gateway (i.e. a real LAN connection).
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up
                              && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(nic => nic.GetIPProperties())
                .Where(props => props.GatewayAddresses.Count > 0)
                .SelectMany(props => props.UnicastAddresses)
                .Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(addr => addr.Address.ToString())
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}
