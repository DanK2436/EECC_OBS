using System;
using System.IO;
using System.Windows;
using EeccStudio.Core.Models;
using Microsoft.Win32;

namespace EeccStudio
{
    public partial class SourcePropertiesWindow : Window
    {
        private readonly StudioSource _source;

        public SourcePropertiesWindow(StudioSource source)
        {
            InitializeComponent();
            _source = source;

            TxtSourceName.Text = source.Name;
            TxtSourceTitle.Text = $"Propriétés : {source.Name}";
            TxtSourceType.Text = $"Type : {source.Type} ({source.Details})";

            TxtContent.Text = source.TextContent;
            TxtSecondary.Text = source.SecondaryText;
            TxtImagePath.Text = source.ImagePath;
            SliderScrollSpeed.Value = source.ScrollSpeed > 0 ? source.ScrollSpeed : 70.0;
            TxtSpeedValue.Text = $"{(int)SliderScrollSpeed.Value} px/s";

            // Initialisation des transformations OBS (Orientation & Miroir)
            CmbRotation.SelectedIndex = source.RotationAngle switch
            {
                90 => 1,
                180 => 2,
                270 => 3,
                _ => 0
            };
            ChkFlipHorizontal.IsChecked = source.FlipHorizontal;
            ChkFlipVertical.IsChecked = source.FlipVertical;

            // Affichage contextuel selon le type
            if (source.Type == SourceType.Image)
            {
                PanelImageOptions.Visibility = Visibility.Visible;
                PanelTextOptions.Visibility = Visibility.Collapsed;
                PanelScrollOptions.Visibility = Visibility.Collapsed;
            }
            else if (source.Type == SourceType.ScrollingTicker)
            {
                PanelImageOptions.Visibility = Visibility.Collapsed;
                PanelTextOptions.Visibility = Visibility.Visible;
                PanelScrollOptions.Visibility = Visibility.Visible;
            }
            else
            {
                PanelImageOptions.Visibility = Visibility.Collapsed;
                PanelTextOptions.Visibility = Visibility.Visible;
                PanelScrollOptions.Visibility = Visibility.Collapsed;
            }
        }

        private void SliderScrollSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtSpeedValue != null)
                TxtSpeedValue.Text = $"{(int)e.NewValue} px/s";
        }

        private void BtnBrowseImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Sélectionner un logo ou une image",
                Filter = "Fichiers Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Tous les fichiers (*.*)|*.*"
            };
            if (dlg.ShowDialog(this) == true)
            {
                TxtImagePath.Text = dlg.FileName;
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            _source.Name = string.IsNullOrWhiteSpace(TxtSourceName.Text) ? _source.Name : TxtSourceName.Text.Trim();
            _source.TextContent = TxtContent.Text.Trim();
            _source.SecondaryText = TxtSecondary.Text.Trim();
            _source.ImagePath = TxtImagePath.Text.Trim();
            _source.ScrollSpeed = SliderScrollSpeed.Value;

            // Enregistrement des transformations OBS
            _source.RotationAngle = CmbRotation.SelectedIndex switch
            {
                1 => 90,
                2 => 180,
                3 => 270,
                _ => 0
            };
            _source.FlipHorizontal = ChkFlipHorizontal.IsChecked == true;
            _source.FlipVertical = ChkFlipVertical.IsChecked == true;

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
