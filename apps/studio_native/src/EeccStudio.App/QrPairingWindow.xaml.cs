using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using EeccStudio.Core.Network;
using EeccStudio.Core.Pairing;

namespace EeccStudio;

public partial class QrPairingWindow : Window
{
    private readonly CameraServer _server;
    private readonly DispatcherTimer _timer;
    private PairingTicket? _ticket;
    private bool _loading = true;

    public QrPairingWindow(CameraServer server)
    {
        InitializeComponent();
        _server = server;
        _server.OnCameraConnected += OnPhonesChanged;
        _server.OnCameraDisconnected += OnPhoneGone;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => RefreshCountdown();
        Loaded += (_, _) => LoadAddresses();
    }

    private void LoadAddresses()
    {
        _loading = true;
        CmbLan.Items.Clear();
        foreach (var address in _server.ListLanAddresses())
            CmbLan.Items.Add(address);

        if (CmbLan.Items.Count == 0)
        {
            ShowUnavailable(_server.IsRunning
                ? "Aucune adresse IPv4 privée utilisable n'a été détectée. Le QR n'est pas affiché et aucune adresse de remplacement n'est inventée."
                : (_server.StartError ?? "Le serveur d'appairage n'écoute pas."));
            _loading = false;
            return;
        }

        CmbLan.SelectedIndex = 0;
        _loading = false;
        IssueTicket();
        _timer.Start();
    }

    private void IssueTicket()
    {
        if (_ticket != null && !_ticket.Consumed)
            _server.RevokePairingTicket(_ticket.Token);
        _ticket = null;
        ImgQrCode.Source = null;

        if (!_server.IsRunning)
        {
            ShowUnavailable(_server.StartError ?? "Le serveur d'appairage n'écoute pas.");
            return;
        }

        var address = CmbLan.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(address))
        {
            ShowUnavailable("Choisissez une adresse privée du Studio.");
            return;
        }

        try
        {
            _ticket = _server.IssuePairingTicket(_server.StudioName, address);
            ImgQrCode.Source = QrCodeGenerator.GenerateQrCode(_ticket.ToQrJson(), 260);
            TxtError.Text = string.Empty;
            RefreshCountdown();
            RefreshPhones();
        }
        catch (Exception ex)
        {
            ShowUnavailable(ex.Message);
        }
    }

    private void RefreshCountdown()
    {
        RefreshPhones();
        if (_ticket == null)
            return;
        if (_ticket.Consumed)
        {
            IssueTicket();
            return;
        }

        var left = _ticket.ExpiresAt - _server.Clock();
        if (left <= TimeSpan.Zero)
        {
            TxtCountdown.Text = "QR expiré — renouvellement";
            IssueTicket();
            return;
        }

        TxtCountdown.Text = $"Valable encore {(int)left.TotalMinutes:00}:{left.Seconds:00} · usage unique";
        TxtServerStatus.Text =
            $"ws://{_ticket.LanIpv4}:{_ticket.Port}/camera/control · Être sur le même Wi-Fi ne suffit pas si le pare-feu ou l'isolation du point d'accès bloque le port.";
    }

    private void RefreshPhones()
    {
        var phones = _server.ConnectedCameras.Values.OrderBy(phone => phone.SlotNumber).ToList();
        if (phones.Count == 0 && _server.ReservedPhoneCount == 0)
        {
            TxtPhones.Text = "En attente du téléphone. Aucun flux n'est simulé.";
            return;
        }

        var builder = new StringBuilder();
        foreach (var phone in phones)
        {
            var battery = phone.BatteryLevel < 0 ? "batterie non reçue" : $"{phone.BatteryLevel} %";
            var frame = _server.GetCameraFrame(phone.StreamName) == null ? "aucune image" : "image reçue";
            builder.AppendLine($"{phone.StreamName} · {phone.DisplayName} · {phone.DeviceModel} · {phone.RemoteIp} · {battery} · {frame}");
        }

        if (_server.ReservedPhoneCount > 0)
            builder.AppendLine($"{_server.ReservedPhoneCount} téléphone(s) coupé(s), session encore récupérable.");
        builder.Append($"Reliés {_server.ConnectedPhoneCount} / {CameraServer.MaxPhoneConnections}. Caméras physiques cataloguées : {_server.PhysicalCameraCount}, non comptées dans les 15.");
        TxtPhones.Text = builder.ToString();
    }

    private void ShowUnavailable(string message)
    {
        TxtError.Text = message;
        TxtCountdown.Text = string.Empty;
        TxtServerStatus.Text = string.Empty;
        ImgQrCode.Source = null;
        TxtPhones.Text = "Aucun QR tant que le serveur et une adresse privée ne sont pas disponibles.";
    }

    private void OnPhonesChanged(CameraClient _) => Dispatcher.Invoke(RefreshCountdown);
    private void OnPhoneGone(string _) => Dispatcher.Invoke(RefreshPhones);

    private void CmbLan_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || !IsLoaded)
            return;
        IssueTicket();
    }

    private void BtnRenew_Click(object sender, RoutedEventArgs e) => IssueTicket();

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_ticket == null || _ticket.Consumed)
        {
            TxtError.Text = "Aucun code valable à copier.";
            return;
        }

        try
        {
            Clipboard.SetText(_ticket.ToQrJson());
            TxtError.Text = "Code temporaire copié. Il expire et ne sert qu'une fois.";
        }
        catch (Exception ex)
        {
            TxtError.Text = "Copie impossible : " + ex.Message;
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _server.OnCameraConnected -= OnPhonesChanged;
        _server.OnCameraDisconnected -= OnPhoneGone;
        if (_ticket != null && !_ticket.Consumed)
            _server.RevokePairingTicket(_ticket.Token);
        base.OnClosed(e);
    }
}
