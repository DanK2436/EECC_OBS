using System;
using System.Text.Json;
using System.Windows;
using EeccStudio.Core.Network;

namespace EeccStudio
{
    public partial class QrPairingWindow : Window
    {
        private readonly CameraServer _server;

        public QrPairingWindow(CameraServer server)
        {
            InitializeComponent();
            _server = server;

            LoadQrCode();
        }

        private void LoadQrCode()
        {
            try
            {
                var localIp = _server.GetLocalIpAddress();
                TxtIpPort.Text = $"{localIp}:{_server.Port}";
                TxtServerStatus.Text = $"● Serveur Caméra Actif : {localIp}:{_server.Port}";

                // Payload JSON exact attendu par EECC Caméra (StudioSessionConfig)
                var payload = new
                {
                    studio_ip = localIp,
                    port = _server.Port,
                    token = _server.PairingToken,
                    stream_name = "Caméra Mobile 1",
                    codec = "h264",
                    bitrate_kbps = 4500,
                    fps = 30,
                    church = _server.ChurchName
                };

                string json = JsonSerializer.Serialize(payload);
                var qrBitmap = QrCodeGenerator.GenerateQrCode(json, 260);
                ImgQrCode.Source = qrBitmap;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de la génération du QR Code : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var localIp = _server.GetLocalIpAddress();
                Clipboard.SetText($"ws://{localIp}:{_server.Port}/camera/control");
                MessageBox.Show("Adresse du serveur Studio copiée dans le presse-papiers !", "Copié", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch { }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
