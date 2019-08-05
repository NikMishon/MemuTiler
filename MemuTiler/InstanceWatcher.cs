using System;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Windows.Threading;

namespace MemuTiler
{
    public class InstanceWatcher : DispatcherObject
    {
        private readonly string _pipeServerName;

        public InstanceWatcher(string pipeServerName, string commandToRemoteInstance = "")
        {
            _pipeServerName = pipeServerName;
            var backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += BackgroundWorkerOnDoWork;

            if (!IsSinglApplication(commandToRemoteInstance))
                throw new Exception("Is application is copied");

            backgroundWorker.RunWorkerAsync();
        }

        private void BackgroundWorkerOnDoWork(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                using (var server = new NamedPipeServerStream(_pipeServerName))
                {
                    server.WaitForConnection();

                    using (var reader = new StreamReader(server))
                        OnRunUnRegisterInstance(reader.ReadToEnd());
                }
            }
        }

        private bool IsSinglApplication(string commandToRemoteInstance)
        {
            try
            {
                using (var client = new NamedPipeClientStream(_pipeServerName))
                {
                    client.Connect(100);

                    if (!string.IsNullOrEmpty(commandToRemoteInstance))
                    {
                        var data = Encoding.UTF8.GetBytes(commandToRemoteInstance);
                        client.Write(data, 0, data.Length);
                    }
                }
            }
            catch (TimeoutException)
            {
                return true;
            }

            return false;
        }

        public event EventHandler<string> RunUnRegisterInstance;

        protected virtual void OnRunUnRegisterInstance(string commandFromRemoteInstance)
        {
            if (RunUnRegisterInstance != null)
                Dispatcher.BeginInvoke(RunUnRegisterInstance, this, commandFromRemoteInstance);
        }
    }
}
