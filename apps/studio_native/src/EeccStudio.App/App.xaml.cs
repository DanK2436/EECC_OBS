using System.IO;
using System.Windows;

namespace EeccStudio
{
    public partial class App : Application
    {
        public static bool IsSmokeTest { get; } =
            string.Equals(Environment.GetEnvironmentVariable("EECC_STUDIO_SMOKE"), "1", StringComparison.Ordinal);

        public static string LogPath { get; } =
            Path.Combine(AppContext.BaseDirectory, "startup.log");

        public static void WriteLog(string message)
        {
            try
            {
                File.AppendAllText(LogPath, DateTime.UtcNow.ToString("o") + " " + message + Environment.NewLine);
            }
            catch
            {
                // Le journal est un diagnostic ; son échec ne doit pas remplacer l'erreur d'origine.
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            WriteLog("OnStartup " + Environment.ProcessPath);
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                WriteLog("UnhandledException " + args.ExceptionObject);
            DispatcherUnhandledException += (_, args) =>
            {
                WriteLog("DispatcherUnhandledException " + args.Exception);
                if (!IsSmokeTest)
                {
                    MessageBox.Show(
                        args.Exception.ToString(),
                        "EECC Studio — erreur au démarrage",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            };
            base.OnStartup(e);
        }
    }
}
