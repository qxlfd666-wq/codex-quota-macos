namespace CodexQuota.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var singleInstance = new Mutex(
            initiallyOwned: true,
            name: @"Local\CodexQuota.Windows",
            createdNew: out var isFirstInstance);
        if (!isFirstInstance)
            return;

        ApplicationConfiguration.Initialize();
        try
        {
            Application.Run(new TrayApplicationContext());
        }
        finally
        {
            singleInstance.ReleaseMutex();
        }
    }
}
