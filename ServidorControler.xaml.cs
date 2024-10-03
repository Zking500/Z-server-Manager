using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace Z_Server_Manager
{
    public partial class ServidorControler : Window
    {
        private SshClient _sshClient;
        private ShellStream _shellStream;

        public ServidorControler(SshClient sshClient)
        {
            InitializeComponent();
            _sshClient = sshClient;
            StartShellStream();
            StartResourceMonitoring(); // Inicia el monitoreo de recursos
        }

        private void StartShellStream()
        {
            var modes = new Dictionary<Renci.SshNet.Common.TerminalModes, uint>();
            _shellStream = _sshClient.CreateShellStream("xterm", 80, 24, 800, 600, 1024, modes);
            Task.Run(() => ReadTerminalOutput());
        }

        private async Task ReadTerminalOutput()
        {
            var reader = new StreamReader(_shellStream);
            while (!_shellStream.DataAvailable && !_sshClient.IsConnected)
            {
                string result = await reader.ReadToEndAsync();
                Dispatcher.Invoke(() =>
                {
                    TerminalOutput.AppendText(result);
                    TerminalOutput.ScrollToEnd();
                });
            }
        }

        private void StartResourceMonitoring()
        {
            Task.Run(() =>
            {
                while (_sshClient.IsConnected)
                {
                    // Obtener el uso de CPU y RAM
                    var cpuCommand = _sshClient.CreateCommand("top -bn1 | grep 'Cpu(s)' | awk '{print $2 + $4}'");
                    var ramCommand = _sshClient.CreateCommand("free -m | awk 'NR==2{printf \"%.2f%%\", $3*100/$2 }'");

                    var cpuUsage = cpuCommand.Execute().Trim();
                    var ramUsage = ramCommand.Execute().Trim();

                    Dispatcher.Invoke(() =>
                    {
                        CpuUsageText.Text = $"CPU Usage: {cpuUsage}%";
                        RamUsageText.Text = $"RAM Usage: {ramUsage}%";
                    });

                    Task.Delay(5000).Wait(); // Actualizar cada 5 segundos
                }
            });
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (_sshClient != null && _sshClient.IsConnected)
            {
                _sshClient.Disconnect();
                _sshClient.Dispose();
            }
        }
    }
}
