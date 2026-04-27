namespace RetroModemBridge;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "RetroModemBridge-startup-error.txt");
            File.WriteAllText(logPath, ex.ToString());

            try
            {
                MessageBox.Show(
                    "RetroModem Bridge could not start.\n\n" +
                    "The error was written to:\n" + logPath,
                    "RetroModem Bridge startup error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
                // If even the message box fails, the log file still has the error.
            }
        }
    }
}
