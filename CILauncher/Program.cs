using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using CILauncher.Core;

namespace CILauncher;

static class Program
{
    [STAThread]
    public static void Main()
    {
        LauncherPaths.RememberExecutable();
        if (StartedByTheGame() && DetachFromTheGame())
            return;

        var app = new App();
        app.InitializeComponent();
        app.Run(new MainWindow());
    }

    private static bool StartedByTheGame()
    {
        try
        {
            var info = new ProcessBasicInformation();
            if (NtQueryInformationProcess(Process.GetCurrentProcess().Handle, 0, ref info, Marshal.SizeOf(typeof(ProcessBasicInformation)), out _) != 0)
                return false;
            using var parent = Process.GetProcessById(info.InheritedFromUniqueProcessId.ToInt32());
            return GameLauncher.IsGameProcessName(parent.ProcessName);
        }
        catch
        {
            return false;
        }
    }

    private static bool DetachFromTheGame()
    {
        var exe = LauncherPaths.CurrentExecutable();
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
            return false;

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = "\"" + exe + "\"",
            UseShellExecute = false
        });
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved2_0;
        public IntPtr Reserved2_1;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        ref ProcessBasicInformation processInformation,
        int processInformationLength,
        out int returnLength);
}
