using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EeccStudio.Core.Media;
using EeccStudio.Core.Pairing;

namespace EeccStudio;

public partial class PairingWindow : Window
{
    private readonly StudioLinkController _link;
    private readonly DispatcherTimer _clock;
    private string? _payload;

    public PairingWindow(StudioLinkController link)
    {
        InitializeComponent();
        _link = link;
        _link.StatusChanged += OnStatus;
        var addresses = LanAddresses.List();
        AddressBox.ItemsSource = addresses;
        AddressBox.DisplayMemberPath = nameof(LanAddress.Label);
        if (addresses.Count > 0) AddressBox.SelectedIndex = 0;
        else StatusText.Text = "Aucune adresse IPv4 locale. Branchez le réseau de la régie.";
        if (_link.ListeningPort is int port && addresses.Count > 0)
            StatusText.Text = "Signalisation TLS en écoute sur le port " + port + ".";
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => RefreshExpiry();
        _clock.Start();
        Closed += (_, _) =>
        {
            _clock.Stop();
            _link.StatusChanged -= OnStatus;
            _payload = null;
        };
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (AddressBox.SelectedItem is not LanAddress address)
        {
            StatusText.Text = "Sélectionnez une adresse locale.";
            return;
        }

        var error = _link.IssueQr(address.Ip);
        if (error != null)
        {
            StatusText.Text = error;
            return;
        }

        _payload = _link.CurrentQrPayload;
        if (string.IsNullOrEmpty(_payload))
        {
            StatusText.Text = "QR vide.";
            return;
        }

        var png = PairingQr.ToPng(_payload);
        using var stream = new System.IO.MemoryStream(png);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        QrImage.Source = image;
        CopyButton.IsEnabled = true;
        RefreshExpiry();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_payload)) return;
        Clipboard.SetText(_payload);
        StatusText.Text = "Texte du QR copié. Il contient le jeton : ne le collez pas dans un journal.";
    }

    private void RefreshExpiry()
    {
        var expires = _link.QrExpiresAt;
        if (expires == null || string.IsNullOrEmpty(_payload))
        {
            ExpiryText.Text = "Aucun QR actif.";
            return;
        }

        var left = expires.Value - DateTimeOffset.UtcNow;
        ExpiryText.Text = left <= TimeSpan.Zero
            ? "Ce QR n'est plus valable. Générez-en un nouveau."
            : "Valable encore " + (int)left.TotalMinutes + " min " + left.Seconds.ToString("00") + " s. Usage unique.";
    }

    private void OnStatus(string status)
    {
        Dispatcher.BeginInvoke(() => StatusText.Text = status);
    }
}
