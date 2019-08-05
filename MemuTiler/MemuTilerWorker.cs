using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using MemuTilerDTO;

namespace MemuTiler
{
    public class MemuTilerWorker
    {
        private readonly List<WorkerRecord> _recordsInWork = new List<WorkerRecord>();

        private WorkerRecord GetRecordInWork(string proc, string pattern) =>
            _recordsInWork.SingleOrDefault(t => t.Settings.Proc == proc && t.Settings.TitleMask == pattern);

        private WorkerRecord GetRecordInWork(SettingsRecord record) => GetRecordInWork(record.Proc, record.TitleMask);

        public bool IsWork(string proc, string pattern) =>
            _recordsInWork.Any(t => t.Settings.Proc == proc && t.Settings.TitleMask == pattern);

        public bool IsWork(SettingsRecord record) => IsWork(record.Proc, record.TitleMask);

        public bool DoWork(SettingsRecord record)
        {
            try
            {
                if (IsWork(record))
                    return false;

                var workerRecord = new WorkerRecord(record);

                _recordsInWork.Add(workerRecord);
            }
            catch (Exception e)
            {
                MessageBox.Show(Application.Current.MainWindow, e.ToString(), "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        public bool StopWork(SettingsRecord record)
        {
            if (!IsWork(record))
                return false;

            var inWork = GetRecordInWork(record);

            _recordsInWork.Remove(inWork);

            inWork.Dispose();

            return true;
        }

        public void TileMemu()
        {
            var xStart = 0;
            foreach (var memuRecord in _recordsInWork)
                xStart = memuRecord.Tile(xStart);
        }
    }
}
