using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Cloudless
{
    public partial class GifWarningWindow : Window
    {
        private string _imagePath;
        private double _fileSizeMB;
        private readonly string _originalCopyButtonText;
        private CancellationTokenSource? _copyAnimationCancellationTokenSource;
        public bool Proceed = false;

        public GifWarningWindow(string imagePath, double fileSizeMB)
        {
            InitializeComponent();
            _imagePath = imagePath;
            _fileSizeMB = fileSizeMB;
            LoadImageInfo();
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e) { WindowHelper.HandleMouseDown(this, e); }
        private void Window_KeyDown(object sender, KeyEventArgs e) { WindowHelper.HandleKeyDown(this, e); }
        private void Close_Click(object sender, RoutedEventArgs e) { WindowHelper.Close_Click(this, e); }

        private void LoadImageInfo()
        {
            if (string.IsNullOrEmpty(_imagePath))
                return;

            FilenameText.Text = $"{_imagePath}";
            SizeText.Text = $"{_fileSizeMB:0.##} MB";
        }

        private void Proceed_Click(object sender, RoutedEventArgs e)
        {
            Proceed = true;
            Close();
        }

        private void Reveal_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.RevealImageInExplorer(_imagePath);
        }
    }
}
