using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MemuTiler.Annotations;
using MemuTilerDTO;

namespace MemuTiler
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<SettingsRecordViewModel> _records;
        private bool _isAutoRun;

        public bool IsAutoRun
        {
            get => _isAutoRun;
            set
            {
                _isAutoRun = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<SettingsRecordViewModel> Records
        {
            get => _records;
            set
            {
                _records = value;
                OnPropertyChanged();
            }
        }

        public SettingsViewModel(Settings settings)
        {
            IsAutoRun = settings.IsAutoRun;
            Records = new ObservableCollection<SettingsRecordViewModel>(
                settings.Record.Select(t => new SettingsRecordViewModel(t)));

            Records.CollectionChanged += RecordsOnCollectionChanged;

            foreach (var settingsRecordViewModel in Records)
                settingsRecordViewModel.PropertyChanged += SettingsRecordViewModelOnPropertyChanged;
        }

        private void RecordsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e?.OldItems != null)
                foreach (var item in e.OldItems.OfType<SettingsRecordViewModel>())
                    item.PropertyChanged -= SettingsRecordViewModelOnPropertyChanged;

            if (e?.NewItems != null)
                foreach (var item in e.NewItems.OfType<SettingsRecordViewModel>())
                    item.PropertyChanged += SettingsRecordViewModelOnPropertyChanged;
        }

        private void SettingsRecordViewModelOnPropertyChanged(object sender, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(Records));

        public Settings ToTransferData()
        {
            return new Settings()
            {
                IsAutoRun = IsAutoRun,
                Record = Records.Select(t => t.ToTransferData()).ToList(),
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SettingsRecordViewModel : INotifyPropertyChanged
    {
        private string _proc;
        private string _titleMask;
        private int _groupNumber;
        private bool _isTileHorizontalWin;
        private bool _isAutoRun;
        private PointViewModel _size;
        private UpdateRateViewModel _updateRate;
        private bool _isRun;

        public string Proc
        {
            get => _proc;
            set
            {
                _proc = value;
                OnPropertyChanged();
            }
        }

        public string TitleMask
        {
            get => _titleMask;
            set
            {
                if (IsRun) MediaCommands.Stop.Execute(this, null);

                GroupNumber = 0;

                _titleMask = value;
                OnPropertyChanged();
            }
        }

        public int GroupNumber
        {
            get => _groupNumber;
            set
            {
                _groupNumber = value;
                OnPropertyChanged();
            }
        }

        public bool IsTileHorizontalWin
        {
            get => _isTileHorizontalWin;
            set
            {
                if (IsRun) MediaCommands.Stop.Execute(this, null);

                _isTileHorizontalWin = value;
                OnPropertyChanged();
            }
        }

        public bool IsAutoRun
        {
            get => _isAutoRun;
            set
            {
                _isAutoRun = value;
                OnPropertyChanged();
            }
        }

        public PointViewModel Size
        {
            get => _size;
            set
            {
                if (IsRun) MediaCommands.Stop.Execute(this, null);

                if (_size != null)
                    _size.PropertyChanged -= SizeOnPropertyChanged;

                _size = value;

                if (_size != null)
                    _size.PropertyChanged += SizeOnPropertyChanged;
                OnPropertyChanged();
            }
        }

        private void SizeOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (IsRun) MediaCommands.Stop.Execute(this, null);

            OnPropertyChanged(nameof(Size));
        }

        public UpdateRateViewModel UpdateRate
        {
            get => _updateRate;
            set
            {
                if (IsRun) MediaCommands.Stop.Execute(this, null);

                if (_updateRate != null)
                    _updateRate.PropertyChanged -= UpdateRateOnPropertyChanged;

                _updateRate = value;

                if (_updateRate != null)
                    _updateRate.PropertyChanged += UpdateRateOnPropertyChanged;

                OnPropertyChanged();
            }
        }

        private void UpdateRateOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (IsRun) MediaCommands.Stop.Execute(this, null);

            OnPropertyChanged(nameof(UpdateRate));
        }

        public bool IsRun
        {
            get => _isRun;
            set
            {
                _isRun = value;
                OnPropertyChanged();
            }
        }

        public SettingsRecordViewModel(SettingsRecord record)
        {
            Proc = record.Proc;
            TitleMask = record.TitleMask;
            GroupNumber = record.GroupNumber;
            IsTileHorizontalWin = record.IsTileHorizontalWin;
            IsAutoRun = record.IsAutoRun;
            Size = new PointViewModel(record.Size);
            UpdateRate = new UpdateRateViewModel(record.UpdateRate);
        }

        public SettingsRecord ToTransferData()
        {
            return new SettingsRecord()
            {
                Proc = Proc,
                TitleMask = TitleMask,
                GroupNumber = GroupNumber,
                IsTileHorizontalWin = IsTileHorizontalWin,
                IsAutoRun = IsAutoRun,
                Size = Size.ToTransferData(),
                UpdateRate = UpdateRate.ToTransferData()
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class UpdateRateViewModel : INotifyPropertyChanged
    {
        private RateUnits _units;
        private long _value;

        public RateUnits Units
        {
            get => _units;
            set
            {
                _units = value;
                OnPropertyChanged();
            }
        }

        public long Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged();
            }
        }

        public UpdateRateViewModel(UpdateRate updateRate)
        {
            Units = updateRate.Units;
            Value = updateRate.Value;
        }

        public UpdateRate ToTransferData()
        {
            return new UpdateRate()
            {
                Units = Units,
                Value = Value
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class PointViewModel : INotifyPropertyChanged
    {
        private int _x;
        private int _y;

        public int X
        {
            get => _x;
            set
            {
                _x = value;
                OnPropertyChanged();
            }
        }

        public int Y
        {
            get => _y;
            set
            {
                _y = value;
                OnPropertyChanged();
            }
        }

        public PointViewModel(Point point)
        {
            X = point.X;
            Y = point.Y;
        }

        public Point ToTransferData()
        {
            return new Point()
            {
                X = X,
                Y = Y
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public static class UpdateRateExtention
    {
        public static TimeSpan ToTimeSpan(this UpdateRate rate)
        {
            switch (rate.Units)
            {
                case RateUnits.milliseconds:
                    return TimeSpan.FromMilliseconds(rate.Value);
                case RateUnits.seconds:
                    return TimeSpan.FromSeconds(rate.Value);
                case RateUnits.minutes:
                    return TimeSpan.FromMinutes(rate.Value);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
