using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using System.IO;

namespace InputSoundPlugin
{
    public partial class SettingView : UserControl
    {
        private readonly PluginSettings _settings;
        private bool _isWaitingInput = false;

        public SettingView(PluginSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            DataContext = settings;
            CollectionViewSource.GetDefaultView(_settings.KeySettings).Filter = obj => FilterFunc((obj as KeySoundSetting)?.Name);
            CollectionViewSource.GetDefaultView(_settings.MouseSettings).Filter = obj => FilterFunc((obj as MouseSoundSetting)?.Name);
        }
        private bool FilterFunc(string? name) => string.IsNullOrWhiteSpace(SearchBox.Text) || (name?.IndexOf(SearchBox.Text, StringComparison.OrdinalIgnoreCase) >= 0);
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) { CollectionViewSource.GetDefaultView(_settings.KeySettings).Refresh(); CollectionViewSource.GetDefaultView(_settings.MouseSettings).Refresh(); }

        private void WaitButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isWaitingInput) return;
            _isWaitingInput = true;
            WaitButton.Content = "入力待機中...";

            Window window = Window.GetWindow(this);
            if (window != null)
            {
                window.PreviewKeyDown += OnWaitKeyDown;
                window.PreviewMouseDown += OnWaitMouseDown;
                FocusManager.SetFocusedElement(window, window);
            }
        }

        private void OnWaitKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            if (e.Key != Key.Escape)
            {
                SearchBox.Text = (e.Key == Key.System ? e.SystemKey : e.Key).ToString();
            }
            EndWaiting();
        }

        private void OnWaitMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            SearchBox.Text = e.ChangedButton.ToString();
            EndWaiting();
        }

        private void EndWaiting()
        {
            Window window = Window.GetWindow(this);
            if (window != null)
            {
                window.PreviewKeyDown -= OnWaitKeyDown;
                window.PreviewMouseDown -= OnWaitMouseDown;
            }
            WaitButton.Content = "キー入力で検索";
            _isWaitingInput = false;
        }

        private void Reset_Click(object sender, RoutedEventArgs e) => _settings.ResetAllSettings(true);
        private void ApplyFolderToAll_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { CheckFileExists = false, FileName = "フォルダ選択" };
            if (dialog.ShowDialog() == true)
            {
                string path = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
                if (string.IsNullOrEmpty(path)) return;
                foreach (var s in _settings.KeySettings) if (!s.Paths.Contains(path)) s.Paths.Add(path);
                foreach (var s in _settings.MouseSettings) if (!s.Paths.Contains(path)) s.Paths.Add(path);
            }
        }
        private void AddPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "音声・動画|*.wav;*.mp3;*.mp4;*.avi;*.wmv|全ファイル|*.*", CheckFileExists = false };
            if (dialog.ShowDialog() == true) ((sender as Button)?.DataContext as ISoundSetting)?.Paths.Add(dialog.FileName);
        }
        private void RemovePath_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is string path)
            {
                foreach (var s in _settings.KeySettings) if (s.Paths.Contains(path)) { s.Paths.Remove(path); return; }
                foreach (var s in _settings.MouseSettings) if (s.Paths.Contains(path)) { s.Paths.Remove(path); return; }
            }
        }
    }
}