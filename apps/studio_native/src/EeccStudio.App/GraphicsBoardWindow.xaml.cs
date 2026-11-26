using System.IO;
using System.Windows;
using System.Windows.Controls;
using EeccStudio.Core.Models;
using Microsoft.Win32;

namespace EeccStudio;

public partial class GraphicsBoardWindow : Window
{
    private readonly StudioScene _scene;
    private readonly OverlayPlayback _preview = new();
    private StudioSource? _current;
    private bool _loading;

    public event Action? Applied;

    public GraphicsBoardWindow(StudioScene scene, StudioSource? focus = null)
    {
        InitializeComponent();
        _scene = scene;
        ComboStyle.ItemsSource = Choices(
            ("Classique", GraphicTextStyle.Classic),
            ("Bandeau", GraphicTextStyle.LowerThird),
            ("Verset", GraphicTextStyle.Verse),
            ("Titre", GraphicTextStyle.Title),
            ("Contour", GraphicTextStyle.Outline),
            ("Verre", GraphicTextStyle.Glass));
        ComboMotion.ItemsSource = Choices(
            ("Fixe", GraphicMotion.Still),
            ("Défilement", GraphicMotion.Scroll),
            ("Fondu", GraphicMotion.Fade),
            ("Glissement", GraphicMotion.Slide),
            ("Frappe", GraphicMotion.Type),
            ("Pulsation", GraphicMotion.Pulse));
        ComboShape.ItemsSource = Choices(
            ("Rectangle", GraphicShape.Rectangle),
            ("Arrondi", GraphicShape.Rounded),
            ("Cercle", GraphicShape.Circle),
            ("Pilule", GraphicShape.Pill));
        ComboStyle.DisplayMemberPath = "Label";
        ComboMotion.DisplayMemberPath = "Label";
        ComboShape.DisplayMemberPath = "Label";
        Loaded += (_, _) =>
        {
            PreviewCanvas.SizeChanged += (_, _) => ShowPreview();
            RefreshList(focus);
        };
        ComboStyle.SelectionChanged += (_, _) => ShowPreview();
        ComboMotion.SelectionChanged += (_, _) => ShowPreview();
        ComboShape.SelectionChanged += (_, _) => ShowPreview();
        TxtPrimary.TextChanged += (_, _) => ShowPreview();
        TxtSecondary.TextChanged += (_, _) => ShowPreview();
    }

    private void RefreshList(StudioSource? focus)
    {
        var items = _scene.Sources.Where(GraphicPlacement.IsGraphic).ToList();
        ListGraphics.ItemsSource = items;
        ListGraphics.DisplayMemberPath = nameof(StudioSource.Name);
        if (focus != null && items.Contains(focus))
            ListGraphics.SelectedItem = focus;
        else if (items.Count > 0)
            ListGraphics.SelectedIndex = 0;
    }

    private void ListGraphics_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListGraphics.SelectedItem is not StudioSource source)
        {
            _current = null;
            return;
        }

        _loading = true;
        _current = source;
        GraphicPlacement.Ensure(source);
        TxtName.Text = source.Name;
        TxtPrimary.Text = source.TextContent;
        TxtSecondary.Text = source.SecondaryText;
        Select(ComboStyle, source.TextStyle);
        Select(ComboMotion, source.Motion);
        Select(ComboShape, source.Shape);
        SliderX.Value = source.XPercent;
        SliderY.Value = source.YPercent;
        SliderW.Value = Math.Clamp(source.WidthPercent, SliderW.Minimum, SliderW.Maximum);
        SliderH.Value = Math.Clamp(source.HeightPercent, SliderH.Minimum, SliderH.Maximum);
        SliderSize.Value = source.FontSize > 0 ? source.FontSize : 16;
        SliderSpeed.Value = source.ScrollSpeed;
        var image = source.Type == SourceType.Image;
        LblPrimary.Text = image ? "Légende" : "Texte";
        LblSecondary.Visibility = image ? Visibility.Collapsed : Visibility.Visible;
        TxtSecondary.Visibility = image ? Visibility.Collapsed : Visibility.Visible;
        BtnBrowse.Visibility = image ? Visibility.Visible : Visibility.Collapsed;
        _loading = false;
        ShowPreview();
    }

    private void AddStill_Click(object sender, RoutedEventArgs e) => Add(new StudioSource
    {
        Name = "Texte",
        Type = SourceType.Text,
        TextContent = "Nouveau texte",
        SecondaryText = "Deuxième ligne",
        TextStyle = GraphicTextStyle.LowerThird,
        Motion = GraphicMotion.Slide,
    });

    private void AddScroll_Click(object sender, RoutedEventArgs e) => Add(new StudioSource
    {
        Name = "Texte défilant",
        Type = SourceType.ScrollingTicker,
        TextContent = "Annonce du culte · bienvenue à l'Église Évangélique du Centre",
        Motion = GraphicMotion.Scroll,
        ScrollSpeed = 70,
    });

    private void AddImage_Click(object sender, RoutedEventArgs e)
    {
        var source = new StudioSource
        {
            Name = "Image",
            Type = SourceType.Image,
            Shape = GraphicShape.Rounded,
            Motion = GraphicMotion.Fade,
        };
        if (PickImage(source))
            Add(source);
    }

    private void Add(StudioSource source)
    {
        GraphicPlacement.Ensure(source);
        _scene.Sources.Add(source);
        RefreshList(source);
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null)
            return;
        if (PickImage(_current))
            ShowPreview();
    }

    private static bool PickImage(StudioSource source)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg;*.webp;*.bmp",
            Title = "Choisir une image",
        };
        if (dialog.ShowDialog() != true)
            return false;
        source.ImagePath = dialog.FileName;
        source.Name = Path.GetFileNameWithoutExtension(dialog.FileName);
        return true;
    }

    private void Slider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded)
            return;
        LblX.Text = $"X  {SliderX.Value:0}";
        LblY.Text = $"Y  {SliderY.Value:0}";
        LblW.Text = $"Largeur  {SliderW.Value:0}";
        LblH.Text = $"Hauteur  {SliderH.Value:0}";
        LblSize.Text = $"Taille du texte  {SliderSize.Value:0}";
        LblSpeed.Text = $"Vitesse  {SliderSpeed.Value:0}";
        ShowPreview();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        WriteCurrent();
        Applied?.Invoke();
        ShowPreview();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null)
            return;
        _scene.Sources.Remove(_current);
        _current = null;
        RefreshList(null);
        Applied?.Invoke();
    }

    private void WriteCurrent()
    {
        if (_current == null)
            return;
        _current.Name = string.IsNullOrWhiteSpace(TxtName.Text) ? _current.Name : TxtName.Text.Trim();
        _current.TextContent = TxtPrimary.Text;
        _current.SecondaryText = TxtSecondary.Text;
        _current.TextStyle = ValueOf(ComboStyle, _current.TextStyle);
        _current.Motion = ValueOf(ComboMotion, _current.Motion);
        _current.Shape = ValueOf(ComboShape, _current.Shape);
        _current.XPercent = SliderX.Value;
        _current.YPercent = SliderY.Value;
        _current.WidthPercent = SliderW.Value;
        _current.HeightPercent = SliderH.Value;
        _current.FontSize = SliderSize.Value;
        _current.ScrollSpeed = SliderSpeed.Value;
        _current.GraphicPlaced = true;
        if (_current.Motion == GraphicMotion.Scroll)
            _current.Type = SourceType.ScrollingTicker;
        else if (_current.Type == SourceType.ScrollingTicker)
            _current.Type = SourceType.Text;
    }

    private void ShowPreview()
    {
        if (_loading || !IsLoaded)
            return;
        WriteCurrent();
        if (_current == null)
        {
            _preview.Stop();
            PreviewCanvas.Children.Clear();
            return;
        }

        _preview.Paint(PreviewCanvas, [_current]);
    }

    private static void Select<T>(ComboBox box, T value) where T : struct
    {
        foreach (Choice<T> item in box.Items)
        {
            if (item.Value.Equals(value))
            {
                box.SelectedItem = item;
                return;
            }
        }
    }

    private static T ValueOf<T>(ComboBox box, T fallback) where T : struct =>
        box.SelectedItem is Choice<T> choice ? choice.Value : fallback;

    private static Choice<T>[] Choices<T>(params (string Label, T Value)[] pairs) where T : struct =>
        pairs.Select(pair => new Choice<T>(pair.Label, pair.Value)).ToArray();

    private sealed record Choice<T>(string Label, T Value) where T : struct;
}
