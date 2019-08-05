using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using MemuTilerDTO;

namespace MemuTiler
{
    public class WorkerRecord : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public SettingsRecord Settings { get; }
        private readonly DispatcherTimer _timer = new DispatcherTimer();

        public WorkerRecord(SettingsRecord record)
        {
            Settings = record;

            try
            {
                SetWinSize();
            }
            catch (Exception e)
            {
                throw new Exception("Invalide parameters", e);
            }

            _timer.Interval = record.UpdateRate.ToTimeSpan();
            _timer.Tick += TimerOnTick;
            _timer.Start();
        }

        public int Tile(int xStart)
        {
            var pattern = new Regex(Settings.TitleMask);

            var hwnds = (from process in Process.GetProcessesByName(Settings.Proc)
                        let match = pattern.Match(process.MainWindowTitle)
                        where match.Success
                        select new {Hwnd = process.MainWindowHandle, GroupValue = match.Groups[Settings.GroupNumber].Value})
                        .OrderBy(t => t.GroupValue, StringNumberComparer.Instance)
                        .Select(t => t.Hwnd);

            foreach (var hwnd in hwnds)
            {
                GetWindowRect(hwnd, out RECT rect);
                var width = rect.Right - rect.Left;
                var heigth = rect.Bottom - rect.Top;

                if (!Settings.IsTileHorizontalWin | width < heigth) // Импликация
                {
                    SetWindowPos(hwnd, IntPtr.Zero, xStart, 0, Settings.Size.X, Settings.Size.Y, 0x0040);
                    xStart += width;
                }
            }

            return xStart;
        }

        private void TimerOnTick(object sender, EventArgs eventArgs)
        {
            try
            {
                SetWinSize();
            }
            catch (Exception)
            {
                _timer.Stop();
            }
        }

        private void SetWinSize()
        {
            var pattern = new Regex(Settings.TitleMask);

            var hwnds = Process.GetProcessesByName(Settings.Proc)
                .Where(t => pattern.IsMatch(t.MainWindowTitle))
                .Select(t => t.MainWindowHandle).ToArray();

            foreach (var hwnd in hwnds)
            {
                GetWindowRect(hwnd, out RECT rect);
                var width = rect.Right - rect.Left;
                var heigth = rect.Bottom - rect.Top;

                if (!Settings.IsTileHorizontalWin | width < heigth) // Импликация
                {
                    if (width != Settings.Size.X || heigth != Settings.Size.Y)
                        SetWindowPos(hwnd, IntPtr.Zero, rect.Left, rect.Top, Settings.Size.X, Settings.Size.Y, 0x0040);
                }
            }
        }

        public void Dispose()
        {
            _timer?.Stop();
        }
    }
}
