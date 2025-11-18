using System.Drawing;
using System.Globalization;
using ActualLab.OS;

namespace ActualLab.Fusion.Tests.Services;

public static class DisplayInfo
{
    public static readonly Rectangle? PrimaryDisplayDimensions;

    static DisplayInfo()
    {
        PrimaryDisplayDimensions = null;
        try {
            switch (OSInfo.Kind) {
                case OSKind.Windows:
                    var p = new Process() {
                        StartInfo = new ProcessStartInfo() {
                            FileName = "cmd.exe",
                            Arguments =
                                "/c powershell -NoProfile -Command " +
                                "\"$sig = '[DllImport(\\\"user32.dll\\\")] public static extern IntPtr GetDC(IntPtr h); " +
                                "[DllImport(\\\"user32.dll\\\")] public static extern int ReleaseDC(IntPtr h, IntPtr dc); " +
                                "[DllImport(\\\"gdi32.dll\\\")] public static extern int GetDeviceCaps(IntPtr hdc, int n);'; " +
                                "Add-Type -MemberDefinition $sig -Name Native -Namespace Win32; " +
                                "$dc = [Win32.Native]::GetDC([IntPtr]::Zero); " +
                                "$w = [Win32.Native]::GetDeviceCaps($dc, 118); " +
                                "$h = [Win32.Native]::GetDeviceCaps($dc, 117); " +
                                "[Win32.Native]::ReleaseDC([IntPtr]::Zero, $dc); " +
                                "Write-Output \\\"$($w) x $($h)\\\"\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };
                    p.Start();
                    p.WaitForExit();
                    var wh = p.StandardOutput.ReadToEnd().TrimEnd()
                        .Split("\r\n").Last()
                        .Split("x", StringSplitOptions.RemoveEmptyEntries);
                    var w = int.Parse(wh[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture);
                    var h = int.Parse(wh[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture);
                    PrimaryDisplayDimensions = new Rectangle(0, 0, w, h);
                    break;
            }
        }
        catch {
            PrimaryDisplayDimensions = null;
        }
    }
}
