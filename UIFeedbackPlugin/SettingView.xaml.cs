using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using System.IO;
using System.Windows.Media;
using System.Text.Json;
using System.Collections;
using System.Globalization;
using System.Windows.Markup;

namespace InputSoundPlugin
{
    public class PathToFileNameConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (value is string path) ? Path.GetFileName(path) : "";
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
        public override object ProvideValue(IServiceProvider serviceProvider) => this;
    }

    public partial class SettingView : UserControl
    {
        private readonly PluginSettings _settings;
        private bool _isWaitingInput = false;
        private bool _isUpdatingUI = false;

        public SettingView(PluginSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            DataContext = settings;
            CollectionViewSource.GetDefaultView(_settings.KeySettings).Filter = obj => FilterItem(obj as ISoundSetting);
            CollectionViewSource.GetDefaultView(_settings.MouseSettings).Filter = obj => FilterItem(obj as ISoundSetting);
        }

        private bool FilterItem(ISoundSetting? setting)
        {
            if (setting == null) return false;
            if (!string.IsNullOrWhiteSpace(SearchBox.Text) && setting.Name.IndexOf(SearchBox.Text, StringComparison.OrdinalIgnoreCase) < 0) return false;
            bool hasConfig = setting.Paths.Count > 0;
            if (FilterConfigured.IsChecked == true && FilterNotConfigured.IsChecked == false && !hasConfig) return false;
            if (FilterNotConfigured.IsChecked == true && FilterConfigured.IsChecked == false && hasConfig) return false;
            if (setting is KeySoundSetting kss)
            {
                bool anyCat = FilterAZ.IsChecked == true || Filter09.IsChecked == true || FilterF.IsChecked == true || FilterCtrl.IsChecked == true || FilterOther.IsChecked == true;
                if (!anyCat) return true;
                var k = kss.Key;
                if (FilterAZ.IsChecked == true && k >= Key.A && k <= Key.Z && kss.Modifiers == ModifierKeys.None) return true;
                if (Filter09.IsChecked == true && (((k >= Key.D0 && k <= Key.D9) || (k >= Key.NumPad0 && k <= Key.NumPad9)) || kss.Modifiers == ModifierKeys.Shift)) return true;
                if (FilterF.IsChecked == true && k >= Key.F1 && k <= Key.F24) return true;
                string n = kss.Name.ToLower();
                if (FilterCtrl.IsChecked == true && (n.Contains("ctrl") || n.Contains("alt") || n.Contains("shift") || n.Contains("win"))) return true;
                if (FilterOther.IsChecked == true)
                {
                    bool isAZ = k >= Key.A && k <= Key.Z;
                    bool is09 = (k >= Key.D0 && k <= Key.D9) || (k >= Key.NumPad0 && k <= Key.NumPad9);
                    bool isF = k >= Key.F1 && k <= Key.F24;
                    bool isCtrl = n.Contains("ctrl") || n.Contains("alt") || n.Contains("shift") || n.Contains("win");
                    if (!isAZ && !is09 && !isF && !isCtrl) return true;
                }
                return false;
            }
            return true;
        }

        private void Filter_Changed(object sender, RoutedEventArgs e) => RefreshFilters();
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshFilters();
        private void RefreshFilters() { CollectionViewSource.GetDefaultView(_settings.KeySettings).Refresh(); CollectionViewSource.GetDefaultView(_settings.MouseSettings).Refresh(); }

        private void FilterButton_Click(object sender, RoutedEventArgs e) => FilterPopup.IsOpen = FilterButton.IsChecked == true;
        private void CloseFilter_Click(object sender, RoutedEventArgs e) { FilterPopup.IsOpen = false; FilterButton.IsChecked = false; }

        private void Selection_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUI) return;
            var list = sender as ListBox;
            if (list == null) return;

            bool isKey = list == KeyListBox;
            var area = isKey ? KeyDetailArea : MouseDetailArea;
            var title = isKey ? KeyDetailTitle : MouseDetailTitle;
            var pathList = isKey ? KeyPathList : MousePathList;
            var press = isKey ? KeyOnPressCheck : MouseOnPressCheck;
            var release = isKey ? KeyOnReleaseCheck : MouseOnReleaseCheck;

            _isUpdatingUI = true;
            if (list.SelectedItems.Count > 0)
            {
                area.Visibility = Visibility.Visible;
                var selectedItems = list.SelectedItems.Cast<ISoundSetting>().ToList();
                var firstItem = selectedItems[0];
                title.Text = selectedItems.Count == 1 ? firstItem.Name : $"{firstItem.Name} (他{selectedItems.Count - 1}件選択中)";
                pathList.ItemsSource = firstItem.Paths;
                press.IsChecked = selectedItems.All(x => x.OnPress);
                release.IsChecked = selectedItems.All(x => x.OnRelease);
            }
            else { area.Visibility = Visibility.Collapsed; }
            _isUpdatingUI = false;
        }

        private void OnPress_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUI) return;
            var list = MainTab.SelectedItem == KeyTab ? KeyListBox : MouseListBox;
            foreach (ISoundSetting item in list.SelectedItems) item.OnPress = (sender as CheckBox)?.IsChecked ?? false;
        }
        private void OnRelease_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUI) return;
            var list = MainTab.SelectedItem == KeyTab ? KeyListBox : MouseListBox;
            foreach (ISoundSetting item in list.SelectedItems) item.OnRelease = (sender as CheckBox)?.IsChecked ?? false;
        }

        private void WaitButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isWaitingInput) return;
            _isWaitingInput = true;
            WaitButton.Content = "入力待機中...";
            Window window = Window.GetWindow(this);
            if (window != null) { window.PreviewKeyDown += OnWaitKeyDown; window.PreviewMouseDown += OnWaitMouseDown; FocusManager.SetFocusedElement(window, window); }
        }
        private void OnWaitKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            if (e.Key != Key.Escape)
            {
                var key = e.Key == Key.ImeProcessed ? e.ImeProcessedKey : (e.Key == Key.System ? e.SystemKey : e.Key);
                var mods = Keyboard.Modifiers;
                var setting = _settings.KeySettings.FirstOrDefault(x => x.Key == key && x.Modifiers == mods);
                SearchBox.Text = setting?.Name ?? key.ToString();
            }
            EndWaiting();
        }
        private void OnWaitMouseDown(object sender, MouseButtonEventArgs e) { e.Handled = true; SearchBox.Text = e.ChangedButton.ToString(); EndWaiting(); }
        private void EndWaiting() { Window window = Window.GetWindow(this); if (window != null) { window.PreviewKeyDown -= OnWaitKeyDown; window.PreviewMouseDown -= OnWaitMouseDown; } WaitButton.Content = "キー入力で検索"; _isWaitingInput = false; }
        private void Reset_Click(object sender, RoutedEventArgs e) => _settings.ResetAllSettings(true);

        private void MainTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource != MainTab) return;
            if (KeyListBox != null) KeyListBox.UnselectAll();
            if (MouseListBox != null) MouseListBox.UnselectAll();
        }

        private void OtherButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.ContextMenu != null) { btn.ContextMenu.PlacementTarget = btn; btn.ContextMenu.IsOpen = true; }
        }

        private void PlaySample_Click(object sender, RoutedEventArgs e) { if ((sender as Button)?.Tag is string path) _settings.PlayDirect(path); }

        private void AddPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Filter = "音声・動画|*.wav;*.mp3;*.mp4;*.avi;*.wmv|全ファイル|*.*", Multiselect = true };
            if (dialog.ShowDialog() == true)
            {
                var list = MainTab.SelectedItem == KeyTab ? KeyListBox : MouseListBox;
                foreach (ISoundSetting setting in list.SelectedItems) { foreach (var file in dialog.FileNames) if (!setting.Paths.Contains(file)) setting.Paths.Add(file); }
            }
        }
        private void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { CheckFileExists = false, ValidateNames = false, FileName = "フォルダ選択" };
            if (dialog.ShowDialog() == true)
            {
                string path = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
                if (string.IsNullOrEmpty(path)) return;
                var list = MainTab.SelectedItem == KeyTab ? KeyListBox : MouseListBox;
                foreach (ISoundSetting setting in list.SelectedItems) if (!setting.Paths.Contains(path)) setting.Paths.Add(path);
            }
        }
        private void RemovePath_Click(object sender, RoutedEventArgs e)
        {
            var list = MainTab.SelectedItem == KeyTab ? KeyListBox : MouseListBox;
            var pathList = MainTab.SelectedItem == KeyTab ? KeyPathList : MousePathList;
            var targets = pathList.SelectedItems.Cast<string>().ToList();
            if (targets.Count == 0 && (sender as FrameworkElement)?.DataContext is string contextPath) targets.Add(contextPath);
            if (targets.Count == 0) return;
            foreach (ISoundSetting setting in list.SelectedItems) { foreach (var path in targets) setting.Paths.Remove(path); }
        }
        private void ClearPaths_Click(object sender, RoutedEventArgs e)
        {
            var list = MainTab.SelectedItem == KeyTab ? KeyListBox : MouseListBox;
            foreach (ISoundSetting setting in list.SelectedItems) setting.Paths.Clear();
        }
        private void CopyPaths_Click(object sender, RoutedEventArgs e)
        {
            var list = MainTab.SelectedItem == KeyTab ? KeyListBox : MouseListBox;
            var pathList = MainTab.SelectedItem == KeyTab ? KeyPathList : MousePathList;
            if (pathList.SelectedItems.Count > 0) _settings.CopyBuffer = pathList.SelectedItems.Cast<string>().ToList();
            else if (list.SelectedItems.Count > 0) _settings.CopyBuffer = (list.SelectedItems[0] as ISoundSetting)?.Paths.ToList();
        }
        private void PastePaths_Click(object sender, RoutedEventArgs e)
        {
            if (_settings.CopyBuffer == null) return;
            var list = MainTab.SelectedItem == KeyTab ? KeyListBox : MouseListBox;
            foreach (ISoundSetting setting in list.SelectedItems) { foreach (var path in _settings.CopyBuffer) if (!setting.Paths.Contains(path)) setting.Paths.Add(path); }
        }

        private void PathList_DragOver(object sender, DragEventArgs e) { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
        private void PathList_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                var list = MainTab.SelectedItem == KeyTab ? KeyListBox : MouseListBox;
                if (list.SelectedItems.Count == 0) return;
                foreach (ISoundSetting setting in list.SelectedItems) { foreach (var file in files) if (!setting.Paths.Contains(file)) setting.Paths.Add(file); }
            }
        }

        private void ExportSetting_Click(object sender, RoutedEventArgs e)
        {
            var list = MainTab.SelectedItem == KeyTab ? KeyListBox : MouseListBox;
            if (list.SelectedItems.Count == 0) return;
            var dialog = new SaveFileDialog { Filter = "JSONファイル|*.json" };
            if (dialog.ShowDialog() == true)
            {
                var exportList = list.SelectedItems.Cast<ISoundSetting>().Select(x => new { x.Name, x.OnPress, x.OnRelease, x.Paths }).ToList();
                File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(exportList));
            }
        }
        private void ImportSetting_Click(object sender, RoutedEventArgs e)
        {
            var list = MainTab.SelectedItem == KeyTab ? KeyListBox : MouseListBox;
            if (list.SelectedItems.Count == 0) return;
            var dialog = new OpenFileDialog { Filter = "JSONファイル|*.json" };
            if (dialog.ShowDialog() == true)
            {
                var json = File.ReadAllText(dialog.FileName);
                try
                {
                    var dataList = JsonSerializer.Deserialize<List<JsonData>>(json);
                    if (dataList == null) return;
                    foreach (ISoundSetting setting in list.SelectedItems)
                    {
                        var data = dataList.FirstOrDefault(d => d.Name == setting.Name) ?? dataList[0];
                        setting.OnPress = data.OnPress; setting.OnRelease = data.OnRelease;
                        setting.Paths.Clear(); foreach (var p in data.Paths) setting.Paths.Add(p);
                    }
                }
                catch { }
            }
        }
        private class JsonData { public string Name { get; set; } = ""; public bool OnPress { get; set; } public bool OnRelease { get; set; } public List<string> Paths { get; set; } = new(); }
    }
}