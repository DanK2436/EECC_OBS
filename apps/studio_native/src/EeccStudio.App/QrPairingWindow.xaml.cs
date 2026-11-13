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
    private readonly PairingBoard _board = new();
    private readonly DispatcherTimer _timer;
    private readonly ShownQr _shownQr = new();
    private bool _loading = true;

    public QrPairingWindow(CameraServer server)
    {
        InitializeComponent();
        _server = server;
        _server.OnCameraConnected += OnPhonesChanged;
        _server.OnCameraDisconnected += OnPhoneGone;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => ApplyDisplay();
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
        ApplyDisplay();
        _timer.Start();
    }

    private void ApplyDisplay()
    {
        RefreshPhones();
        if (!_server.IsRunning)
        {
            ShowUnavailable(_server.StartError ?? "Le serveur d'appairage n'écoute pas.");
            return;
        }

        PairingDisplay display;
        try
        {
            display = _board.Refresh(_server, CmbLan.SelectedItem as string);
        }
        catch (Exception ex)
        {
            ShowUnavailable(ex.Message);
            return;
        }

        TxtCountdown.Text = display.Message;
        TxtServerStatus.Text = display.ShowsQr
            ? "Le QR change après chaque téléphone validé. Un simple affichage ne consomme pas le jeton. Le même Wi-Fi ne garantit pas que le port soit joignable."
            : string.Empty;
        TxtError.Text = display.ShowsQr ? string.Empty : display.Message;
        if (!_shownQr.Apply(display.QrJson))
            return;
        ImgQrCode.Source = display.QrJson == null ? null : QrCodeGenerator.GenerateQrCode(display.QrJson);
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
        _shownQr.Apply(null);
        TxtPhones.Text = "Aucun QR tant que le serveur et une adresse privée ne sont pas disponibles.";
    }

    private void OnPhonesChanged(CameraClient _) => Dispatcher.Invoke(ApplyDisplay);
    private void OnPhoneGone(string _) => Dispatcher.Invoke(ApplyDisplay);

    private void CmbLan_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || !IsLoaded)
            return;
        ApplyDisplay();
    }

    private void BtnRenew_Click(object sender, RoutedEventArgs e)
    {
        _board.RequestReplacement();
        ApplyDisplay();
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_shownQr.Json))
        {
            TxtError.Text = "Aucun code valable à copier.";
            return;
        }

        try
        {
            Clipboard.SetText(_shownQr.Json);
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
        _board.Refresh(_server, null);
        base.OnClosed(e);
    }
}
