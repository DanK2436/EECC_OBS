using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using EeccStudio.Broadcast;
using EeccStudio.Core.Network;

namespace EeccStudio
{
    public partial class SettingsWindow : Window
    {
        private readonly CameraServer _server;
        private readonly DispatcherTimer _captionTimer;
        private bool _ready;

        public SettingsWindow(CameraServer server)
        {
            InitializeComponent();
            _server = server;
            _captionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _captionTimer.Tick += async (_, _) =>
            {
                _captionTimer.Stop();
                await PushCaptionAsync();
            };

            LoadSettings();
        }

        private void LoadSettings()
        {
            var localIp = _server.GetLocalIpAddress();
            TxtLanUrl.Text = localIp.Length == 0
                ? "Aucune adresse IPv4 privée utilisable. Le flux n'est pas annoncé."
                : $"URL locale : http://{localIp}:{_server.ListeningPort}/live/stream";
            TxtChurchName.Text = _server.ChurchName;
            TxtProgramTitle.Text = _server.ProgramTitle;
            TxtSpeaker.Text = _server.Speaker;
            TxtTheme.Text = _server.Theme;
            _ready = true;

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
            _server.Theme = TxtTheme.Text.Trim();
        }

        private void Caption_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!_ready) return;
            ApplySettings();
            _captionTimer.Stop();
            _captionTimer.Start();
        }

        private async Task PushCaptionAsync()
        {
            try
            {
                await EeccTvCatalog.UpdateCaptionAsync(_server.ProgramTitle, _server.Speaker, _server.Theme);
            }
            catch
            {
                // Le texte local reste affiché. Le prochain passage réessaie.
            }
        }

        private async void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            ApplySettings();
            await PushCaptionAsync();
            MessageBox.Show("Paramètres appliqués avec succès !", "EECC Studio", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            ApplySettings();
            await PushCaptionAsync();
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
