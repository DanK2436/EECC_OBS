using System.IO;
using System.Windows;

namespace EeccStudio
{
    public partial class App : Application
    {
        public static bool IsSmokeTest { get; } =
            string.Equals(Environment.GetEnvironmentVariable("EECC_STUDIO_SMOKE"), "1", StringComparison.Ordinal);

        /// <summary>Données d'exemple de la maquette, uniquement si l'opérateur l'a demandé explicitement.</summary>
        public static bool IsDemoMode { get; } =
            string.Equals(Environment.GetEnvironmentVariable("EECC_STUDIO_DEMO"), "1", StringComparison.Ordinal);

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
                    try
                    {
                        Clipboard.SetText(args.Exception.ToString());
                    }
                    catch
                    {
                        // Le presse-papiers peut échouer si occupé par un autre processus
                    }

                    MessageBox.Show(
                        args.Exception.ToString(),
                        "EECC Studio — Erreur (détails copiés dans le presse-papiers)",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                args.Handled = true;
            };
            base.OnStartup(e);
        }
    }
}
