using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using YukkuriMovieMaker.Plugin;

namespace InputSoundPlugin
{
    public class PluginSettings : SettingsBase<PluginSettings>
    {
        private readonly Random _random = new();
        private readonly List<MediaPlayer> _activePlayers = new();
        private bool _isInitialized = false;
        private long _lastKeyTime = 0;
        private long _lastMouseTime = 0;
        private readonly HashSet<Key> _pressedKeys = new();
        private readonly HashSet<MouseButton> _pressedMouseButtons = new();

        public override SettingsCategory Category => SettingsCategory.None;
        public override string Name => "入力サウンド";
        public override bool HasSettingView => true;
        public override object SettingView => new SettingView(this);

        public ObservableCollection<KeySoundSetting> KeySettings { get; set; } = new();
        public ObservableCollection<MouseSoundSetting> MouseSettings { get; set; } = new();
        public List<string>? CopyBuffer { get; set; }

        private bool _isEnabled = true;
        public bool IsEnabled { get => _isEnabled; set => this.Set(ref _isEnabled, value, nameof(IsEnabled)); }

        private int _volume = 25;
        public int Volume { get => _volume; set => this.Set(ref _volume, value, nameof(Volume)); }

        private double _maxDuration = 0.5;
        public double MaxDuration { get => _maxDuration; set => this.Set(ref _maxDuration, value, nameof(MaxDuration)); }

        private int _maxConcurrentSounds = 10;
        public int MaxConcurrentSounds { get => _maxConcurrentSounds; set => this.Set(ref _maxConcurrentSounds, value, nameof(MaxConcurrentSounds)); }

        public override void Initialize()
        {
            if (KeySettings.Count == 0 && MouseSettings.Count == 0) ResetAllSettings(false);
            if (_isInitialized) return;
            EventManager.RegisterClassHandler(typeof(UIElement), UIElement.PreviewKeyDownEvent, new KeyEventHandler(OnKeyDown), true);
            EventManager.RegisterClassHandler(typeof(UIElement), UIElement.PreviewKeyUpEvent, new KeyEventHandler(OnKeyUp), true);
            EventManager.RegisterClassHandler(typeof(UIElement), UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler(OnMouseDown), true);
            EventManager.RegisterClassHandler(typeof(UIElement), UIElement.PreviewMouseUpEvent, new MouseButtonEventHandler(OnMouseUp), true);
            _isInitialized = true;
        }

        public void ResetAllSettings(bool showConfirm)
        {
            if (showConfirm)
            {
                var res = MessageBox.Show("すべての設定を初期化しますか？", "設定リセット", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res != MessageBoxResult.Yes) return;
            }
            KeySettings.Clear();
            var keys = Enum.GetValues(typeof(Key)).Cast<Key>().Where(k => k != Key.None && k != Key.ImeProcessed).Distinct().OrderBy(k => k.ToString());
            foreach (var key in keys) KeySettings.Add(new KeySoundSetting { Key = key });
            AddShiftSymbol(Key.D1, "!"); AddShiftSymbol(Key.D2, "\""); AddShiftSymbol(Key.D3, "#"); AddShiftSymbol(Key.D4, "$");
            AddShiftSymbol(Key.D5, "%"); AddShiftSymbol(Key.D6, "&"); AddShiftSymbol(Key.D7, "'"); AddShiftSymbol(Key.D8, "(");
            AddShiftSymbol(Key.D9, ")"); AddShiftSymbol(Key.OemMinus, "="); AddShiftSymbol(Key.OemPlus, "+"); AddShiftSymbol(Key.Oem5, "|");
            MouseSettings.Clear();
            foreach (MouseButton btn in Enum.GetValues(typeof(MouseButton))) MouseSettings.Add(new MouseSoundSetting { Button = btn });
        }

        private void AddShiftSymbol(Key key, string symbol) => KeySettings.Add(new KeySoundSetting { Key = key, Modifiers = ModifierKeys.Shift, CustomName = $"Shift + {key} ({symbol})" });

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (!IsEnabled || e.IsRepeat) return;
            var key = e.Key == Key.ImeProcessed ? e.ImeProcessedKey : (e.Key == Key.System ? e.SystemKey : e.Key);
            if (_pressedKeys.Contains(key)) return;
            long now = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            if (now - _lastKeyTime < 2) return;
            _lastKeyTime = now;
            _pressedKeys.Add(key);
            ProcessSound(KeySettings.FirstOrDefault(x => x.Key == key && x.Modifiers == Keyboard.Modifiers), true);
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (!IsEnabled) return;
            var key = e.Key == Key.ImeProcessed ? e.ImeProcessedKey : (e.Key == Key.System ? e.SystemKey : e.Key);
            if (!_pressedKeys.Contains(key)) return;
            _pressedKeys.Remove(key);
            ProcessSound(KeySettings.FirstOrDefault(x => x.Key == key && x.Modifiers == Keyboard.Modifiers), false);
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsEnabled) return;
            var btn = e.ChangedButton;
            if (_pressedMouseButtons.Contains(btn)) return;
            long now = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            if (now - _lastMouseTime < 2) return;
            _lastMouseTime = now;
            _pressedMouseButtons.Add(btn);
            ProcessSound(MouseSettings.FirstOrDefault(x => x.Button == btn), true);
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!IsEnabled) return;
            var btn = e.ChangedButton;
            if (!_pressedMouseButtons.Contains(btn)) return;
            _pressedMouseButtons.Remove(btn);
            ProcessSound(MouseSettings.FirstOrDefault(x => x.Button == btn), false);
        }

        private void ProcessSound(ISoundSetting? setting, bool isDown)
        {
            if (setting == null || setting.Paths.Count == 0) return;
            if (isDown && setting.OnPress) PlayRandom(setting.Paths);
            else if (!isDown && setting.OnRelease) PlayRandom(setting.Paths);
        }

        public void PlayDirect(string path)
        {
            if (Directory.Exists(path))
            {
                var files = Directory.EnumerateFiles(path, "*.*").Where(f => {
                    var ext = Path.GetExtension(f).ToLower();
                    return ext == ".wav" || ext == ".mp3" || ext == ".mp4" || ext == ".avi" || ext == ".wmv";
                }).ToArray();
                if (files.Length > 0) path = files[_random.Next(files.Length)]; else return;
            }
            if (!File.Exists(path)) return;
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => {
                var player = new MediaPlayer();
                player.Open(new Uri(Path.GetFullPath(path)));
                player.Volume = Volume / 100.0;
                player.MediaEnded += (s, e) => player.Close();
                player.Play();
            }));
        }

        public void PlayRandom(ObservableCollection<string> paths) { if (paths.Count > 0) PlayDirect(paths[_random.Next(paths.Count)]); }
    }

    public interface ISoundSetting { string Name { get; } bool OnPress { get; set; } bool OnRelease { get; set; } ObservableCollection<string> Paths { get; } }
    public class KeySoundSetting : ISoundSetting { public Key Key { get; set; } public ModifierKeys Modifiers { get; set; } = ModifierKeys.None; public string? CustomName { get; set; } public string Name => CustomName ?? Key.ToString(); public bool OnPress { get; set; } = true; public bool OnRelease { get; set; } = false; public ObservableCollection<string> Paths { get; set; } = new(); }
    public class MouseSoundSetting : ISoundSetting { public MouseButton Button { get; set; } public string Name => Button.ToString(); public bool OnPress { get; set; } = true; public bool OnRelease { get; set; } = false; public ObservableCollection<string> Paths { get; set; } = new(); }
}