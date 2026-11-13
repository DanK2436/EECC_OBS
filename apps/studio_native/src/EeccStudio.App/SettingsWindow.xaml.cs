using System;
using System.IO;
using System.Windows;
using EeccStudio.Core.Network;

namespace EeccStudio
{
    public partial class SettingsWindow : Window
    {
        private readonly CameraServer _server;

        public SettingsWindow(CameraServer server)
        {
            InitializeComponent();
            _server = server;

            LoadSettings();
        }

        private void LoadSettings()
        {
            var localIp = _server.GetLocalIpAddress();
            TxtLanUrl.Text = $"URL locale : http://{localIp}:{_server.Port}/live/stream";
            TxtChurchName.Text = _server.ChurchName;
            TxtProgramTitle.Text = _server.ProgramTitle;
            TxtSpeaker.Text = _server.Speaker;

            var defaultVideoDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "EECC_Enregistrements");
            TxtRecordPath.Text = defaultVideoDir;
        }

        private void BtnBrowseRecord_Click(object sender, RoutedEventArgs e)
        {
            var defaultVideoDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "EECC_Enregistrements");
            Directory.CreateDirectory(defaultVideoDir);
            TxtRecordPath.Text = defaultVideoDir;
            MessageBox.Show($"Dossier d'enregistrement sélectionné :\n{defaultVideoDir}", "Dossier Vidéo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ApplySettings()
        {
            _server.ProgramTitle = TxtProgramTitle.Text.Trim();
            _server.Speaker = TxtSpeaker.Text.Trim();
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            ApplySettings();
            MessageBox.Show("Paramètres appliqués avec succès !", "EECC Studio", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            ApplySettings();
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
