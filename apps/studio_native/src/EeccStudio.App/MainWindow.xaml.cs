using System.IO;
using System.Windows;
using EeccStudio.Core.Audio;
using EeccStudio.Core.Network;

namespace EeccStudio
{
    public partial class MainWindow : Window
    {
        private readonly CameraServer _cameraServer;
        private readonly AudioEngine _audioEngine;

        public MainWindow()
        {
            InitializeComponent();

            _cameraServer = new CameraServer(port: 8089);
            _audioEngine = new AudioEngine();
            _cameraServer.OnCameraConnected += OnCameraConnected;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_cameraServer.TryStart(out var detail))
                {
                    StatusServerText.Text = string.IsNullOrEmpty(detail)
                        ? "Serveur Caméra Mobile actif (port 8089)"
                        : "Serveur Caméra Mobile — " + detail;
                    if (!string.IsNullOrEmpty(detail))
                        App.WriteLog("CameraServer " + detail);
                }
                else
                {
                    StatusServerText.Text = "Serveur Caméra indisponible";
                    App.WriteLog("CameraServer failed " + detail);
                    if (!App.IsSmokeTest)
                    {
                        MessageBox.Show(
                            detail,
                            "EECC Studio — serveur caméra",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }

                File.WriteAllText(
                    Path.Combine(AppContext.BaseDirectory, "startup.ready"),
                    DateTime.UtcNow.ToString("o"));
                App.WriteLog("MainWindow loaded");
            }
            catch (Exception ex)
            {
                App.WriteLog(ex.ToString());
                StatusServerText.Text = "Erreur au démarrage";
                if (!App.IsSmokeTest)
                {
                    MessageBox.Show(
                        ex.ToString(),
                        "EECC Studio — erreur au démarrage",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void OnCameraConnected(CameraClient camera)
        {
            Dispatcher.Invoke(() =>
            {
                // Notification de couplage caméra en temps réel
            });
        }

        private void BtnTransition_Click(object sender, RoutedEventArgs e)
        {
            // Transition Cut directe sub-frame
        }

        private void BtnPairQr_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "EECC Studio — Serveur Caméra Mobile Actif\n\n" +
                "Adresse locale : ws://192.168.1.100:8089/camera/control\n" +
                "Token d'appairage : EECC-LIVE-4982\n\n" +
                "Pointez EECC Caméra vers le QR Code pour vous connecter instantanément.",
                "Appairage EECC Caméra",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        protected override void OnClosed(System.EventArgs e)
        {
            _cameraServer.Stop();
            base.OnClosed(e);
        }
    }
}
