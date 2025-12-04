using System.Net;
using System.Net.Sockets;

namespace ActualLab.Testing.Web;

public static class WebTestHelpers
{
    public static Uri GetLocalUri(int port, string protocol = "http")
        => new($"{protocol}://127.0.0.1:{port}");

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
