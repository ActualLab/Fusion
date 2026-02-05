using System.Net;
using System.Net.Sockets;

namespace ActualLab.Testing.Web;

/// <summary>
/// Utility methods for web-related test setup, including finding unused TCP ports
/// and creating local URIs.
/// </summary>
public static class WebTestHelpers
{
    public static Uri GetLocalUri(int port, string protocol = "http")
        => new($"{protocol}://127.0.0.1:{port}");

    public static Uri GetUnusedLocalUri(string protocol = "http")
        => GetLocalUri(GetUnusedTcpPort(), protocol);

    public static int GetUnusedTcpPort()
    {
        var listener = new TcpListener(IPAddress.Any, 0);
        listener.Start();
        try {
            return ((IPEndPoint) listener.LocalEndpoint).Port;
        }
        finally {
            listener.Stop();
        }
    }
}
