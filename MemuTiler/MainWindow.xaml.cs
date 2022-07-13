using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using System.Xml;
using System.Xml.Serialization;
using MemuTilerDTO;
using Microsoft.Win32;
using IWshRuntimeLibrary;
using PcInputX;
using File = System.IO.File;

namespace MemuTiler
{
    public partial class MainWindow
    {
        public NotifyIcon NotifyIcon { get; }

        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(
            "Settings", typeof(SettingsViewModel), typeof(MainWindow), new PropertyMetadata(default(SettingsViewModel)));

        public SettingsViewModel Settings
        {
            get => (SettingsViewModel)GetValue(SettingsProperty);
            set => SetValue(SettingsProperty, value);
        }

        private bool _isCancel = true;
        private readonly MemuTilerWorker _memuTilerWorker = new MemuTilerWorker();

        public MainWindow()
        {
            InitializeComponent();

            NotifyIcon = new NotifyIcon
            {
                Icon = Properties.Resources.MEmuConsole,
                ContextMenu = CreateContextMenu()
            };

            NotifyIcon.DoubleClick += NotifyIconOnDoubleClick;

            CommandManager.RegisterClassCommandBinding(GetType(), new CommandBinding(ApplicationCommands.New, NewCommandExecuted));
            CommandManager.RegisterClassCommandBinding(GetType(), new CommandBinding(ApplicationCommands.Delete, DeleteCommandExecuted));

            CommandManager.RegisterClassCommandBinding(GetType(), new CommandBinding(MediaCommands.Play, PlayCommandExecuted, PlayCommandCanExecute));
            CommandManager.RegisterClassCommandBinding(GetType(), new CommandBinding(MediaCommands.Stop, StopCommandExecuted, StopCommandCanExecute));
        }

        private void StopCommandCanExecute(object o, CanExecuteRoutedEventArgs e)
        {
            var recordViewModel = e.Parameter as SettingsRecordViewModel;
            if (recordViewModel == null) return;

            e.CanExecute = _memuTilerWorker.IsWork(recordViewModel.ToTransferData());
        }

        private void StopCommandExecuted(object o, ExecutedRoutedEventArgs e)
        {
            var recordViewModel = e.Parameter as SettingsRecordViewModel;
            if (recordViewModel == null)
            {
                var settings = e.Parameter as SettingsRecord;
                if (settings == null)
                    return;

                recordViewModel = Settings.Records.FirstOrDefault(t => t.Proc == settings.Proc && t.TitleMask == settings.TitleMask);
            }

            if (recordViewModel != null)
                recordViewModel.IsRun = !_memuTilerWorker.StopWork(recordViewModel.ToTransferData());
        }

        private void PlayCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            var recordViewModel = e.Parameter as SettingsRecordViewModel;
            if (recordViewModel == null) return;

            e.CanExecute = !_memuTilerWorker.IsWork(recordViewModel.ToTransferData());
        }

        private void PlayCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            var recordViewModel = e.Parameter as SettingsRecordViewModel;
            if (recordViewModel == null) return;

            recordViewModel.IsRun = _memuTilerWorker.DoWork(recordViewModel.ToTransferData());
        }

        private void DeleteCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            var record = e.Parameter as SettingsRecordViewModel;
            if (record == null) return;

            if (Settings.Records.Contains(record))
                Settings.Records.Remove(record);
        }

        private void NewCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            Settings.Records.Add(new SettingsRecordViewModel(EmptySettingsRecord()));
        }

        private ContextMenu CreateContextMenu()
        {
            return new ContextMenu(new[]
            {
                new MenuItem(@"Тайлить Memu\Momo", TileMemuItem_OnClick),
                new MenuItem("Exit", (sender, args) =>
                {
                    _isCancel = false;
                    NotifyIcon.Visible = false;
                    Close();
                }),
            });
        }

        private void NotifyIconOnDoubleClick(object sender, EventArgs eventArgs)
        {
            Show();
            WindowState = WindowState.Normal;
            NotifyIcon.Visible = false;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                this.Hide();
                NotifyIcon.Visible = true;
            }

            base.OnStateChanged(e);
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            var argv = string.Join(" ", Environment.GetCommandLineArgs().Skip(1));

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            Title = $"Memu tiler {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";

            if (Properties.Settings.Default.TilerSettings == null)
                InitTilerSettings();
            else
            {
                using (var ms = new MemoryStream())
                {
                    Properties.Settings.Default.TilerSettings.Save(ms);
                    ms.Position = 0;
                    var serializer = new XmlSerializer(typeof(Settings));
                    Settings = new SettingsViewModel((Settings)serializer.Deserialize(ms));

                    if (Settings != null)
                        Settings.PropertyChanged += SettingsOnPropertyChanged;
                }
            }

            var currentProcess = Process.GetCurrentProcess();

            if (Settings.IsAutoRun)
            {
                var reg = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run\\");
                reg?.SetValue("MemuTiler", currentProcess.MainModule.FileName);
                reg?.Close();
            }
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = currentProcess.MainModule.FileName,
                    Arguments = argv,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process.Start(processInfo);
                Environment.Exit(0);
            }

            try
            {
                var watcher = new InstanceWatcher("memutilerpipeserver", argv);
                watcher.RunUnRegisterInstance += WatcherOnRunUnRegisterInstance;
            }
            catch (Exception)
            {
                Environment.Exit(0);
            }

            foreach (var recordViewModel in Settings.Records.Where(t => t.IsAutoRun))
                recordViewModel.IsRun = _memuTilerWorker.DoWork(recordViewModel.ToTransferData());

            CheckArguments(argv);

            var bShiftPressed = false;

            KeyboardX.Hook();
            KeyboardX.KeyboardEvent += delegate(object o, KeyboardXEvent ev)
            {
                switch (ev.Key)
                {
                    case Keys.LShiftKey:
                    case Keys.RShiftKey:
                        bShiftPressed = !ev.EventFlags.HasFlag(KeyboardX.KbdllhookstructFlags.LLKHF_UP);
                        break;
                    case Keys.Q:
                    {
                        if (ev.EventFlags.HasFlag(KeyboardX.KbdllhookstructFlags.LLKHF_UP) &&
                            ev.EventFlags.HasFlag(KeyboardX.KbdllhookstructFlags.LLKHF_ALTDOWN) &&
                            bShiftPressed)
                        {
                            _memuTilerWorker.TileMemu();
                        }

                        break;
                    }
                }
            };
        }

        private void CheckArguments(string argv)
        {
            switch (argv)
            {
                case "--tile":
                    _memuTilerWorker.TileMemu();
                    break;
                default:
                    if (NotifyIcon.Visible)
                    {
                        Show();
                        WindowState = WindowState.Normal;
                        NotifyIcon.Visible = false;
                    }

                    Topmost = true;
                    Topmost = false;
                    break;
            }
        }

        private void WatcherOnRunUnRegisterInstance(object o, string commandFromRemoteInstance) => CheckArguments(commandFromRemoteInstance);

        private void InitTilerSettings()
        {
            var settings = new Settings()
            {
                IsAutoRun = true,
                Record = new List<SettingsRecord>()
                {
                    new SettingsRecord()
                    {
                        Proc = "Memu",
                        TitleMask = @"\((\d*)_\w*\)",
                        GroupNumber = 1,
                        IsTileHorizontalWin = true,
                        Size = new MemuTilerDTO.Point()
                        {
                            X = 480,
                            Y = 816
                        },
                        UpdateRate = new UpdateRate()
                        {
                            Units = RateUnits.milliseconds,
                            Value = 400
                        },
                        IsAutoRun = true
                    },
                    new SettingsRecord()
                    {
                        Proc = "dnplayer",
                        TitleMask = @"(\d*)_\w*_MOMO",
                        GroupNumber = 1,
                        IsTileHorizontalWin = true,
                        Size = new MemuTilerDTO.Point()
                        {
                            X = 480,
                            Y = 816
                        },
                        UpdateRate = new UpdateRate()
                        {
                            Units = RateUnits.milliseconds,
                            Value = 400
                        },
                        IsAutoRun = true
                    }
                }
            };

            if (Settings != null)
                Settings.PropertyChanged -= SettingsOnPropertyChanged;

            Settings = new SettingsViewModel(settings);

            if (Settings != null)
                Settings.PropertyChanged += SettingsOnPropertyChanged;
        }

        private void SaveSettings()
        {
            using (var ms = new MemoryStream())
            {
                var serializer = new XmlSerializer(typeof(MemuTilerDTO.Settings));
                serializer.Serialize(ms, Settings.ToTransferData());
                ms.Position = 0;
                var document = new XmlDocument();
                document.Load(ms);

                Properties.Settings.Default.TilerSettings = document;
                Properties.Settings.Default.Save();
            }
        }

        private void SettingsOnPropertyChanged(object o, PropertyChangedEventArgs e) => SaveSettings();

        private SettingsRecord EmptySettingsRecord() => new SettingsRecord()
        {
            Proc = "notepad",
            TitleMask = ".*",
            IsTileHorizontalWin = false,
            Size = new MemuTilerDTO.Point()
            {
                X = 600,
                Y = 800
            },
            UpdateRate = new UpdateRate()
            {
                Units = RateUnits.milliseconds,
                Value = 400
            },
            IsAutoRun = false
        };

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
#if DEBUG
            _isCancel = false;
#endif

            if ((e.Cancel = _isCancel) == true)
            {
                WindowState = WindowState.Minimized;
            }
            else
            {
                SaveSettings();
                KeyboardX.UnHook();
            }
        }

        private void DeleteAutoRunButton_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var reg = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run\\");
                reg?.DeleteValue("MemuTiler");
                reg?.Close();

                IsAutoRunCheckBox.IsChecked = false;
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void IsAutoRunCheckBox_OnChecked(object sender, RoutedEventArgs e)
        {
            var currentProcess = Process.GetCurrentProcess();
            var reg = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run\\");
            reg?.SetValue("MemuTiler", currentProcess.MainModule.FileName);
            reg?.Close();
        }

        private void TileMemuItem_OnClick(object sender, EventArgs e)
        {
            _memuTilerWorker.TileMemu();
        }

        private void ResetSettingsItem_OnClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;

            InitTilerSettings();

            var currentProcess = Process.GetCurrentProcess();
            var processInfo = new ProcessStartInfo
            {
                FileName = currentProcess.MainModule.FileName,
                UseShellExecute = true,
                Verb = "runas"
            };

            Process.Start(processInfo);

            _isCancel = false;
            Close();
        }

        private void AppShortcutToDesktop(string linkName, string commandLineArguments = null)
        {
            var shell = new WshShell();

            var shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), $"{linkName}.lnk");

            if (File.Exists(shortcutPath))
                File.Delete(shortcutPath);

            var shortcutMain = (IWshShortcut)shell.CreateShortcut(shortcutPath);
            shortcutMain.TargetPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            shortcutMain.Arguments = commandLineArguments;

            shortcutMain.Save();
        }

        private void UpdateAllAppShortcuts()
        {
            AppShortcutToDesktop("MemuTiler");
            AppShortcutToDesktop("Tile all", "--tile");
        }

        private void AddShortcutToDesktopMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            UpdateAllAppShortcuts();
        }
    }

    public class RegexToComboboxGroupsItemsSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var regex = new Regex(value?.ToString() ?? string.Empty);
                return regex.GetGroupNumbers().Select(t => t == 0 ? "Весь текст" : $"Группа {t}");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return new[] { "Весь текст" };
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}