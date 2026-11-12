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

            // Initialisation des moteurs C# / C++ natifs
            _cameraServer = new CameraServer(port: 8089);
            _audioEngine = new AudioEngine();

            _cameraServer.OnCameraConnected += OnCameraConnected;
            _cameraServer.Start();
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
