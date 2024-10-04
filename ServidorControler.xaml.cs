using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using static Z_Server_Manager.MainWindow;

namespace Z_Server_Manager
{
    public partial class ServidorControler : Window
    {
        private SshClient _sshClient;
        private ShellStream _shellStream;

        public ServidorControler()
        {
            InitializeComponent();
            LoadCredentialsAndConnect();
        }

        private void LoadCredentialsAndConnect()
        {
            // Cargar credenciales desde el archivo
            Credentials credentials = null;
            if (File.Exists(CredentialsFilePath))
            {
                var json = File.ReadAllText(CredentialsFilePath);
                credentials = JsonSerializer.Deserialize<Credentials>(json);
            }

            if (credentials == null)
            {
                MessageBox.Show("No se encontraron credenciales. Por favor, inicie sesión nuevamente.");
                this.Close();
            }

            // Establecer la conexión SSH si no está conectada
            if (_sshClient == null || !_sshClient.IsConnected)
            {
                _sshClient = new SshClient(credentials.ServerIp, credentials.Port, credentials.Username, credentials.Password);
                try
                {
                    _sshClient.Connect();
                    StartShellStream();
                    StartResourceMonitoring();  // Iniciar monitoreo de recursos
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al conectar: {ex.Message}");
                }
            }
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
            while (_sshClient.IsConnected)
            {
                char[] buffer = new char[1024];
                int read = await reader.ReadAsync(buffer, 0, buffer.Length);
                if (read > 0)
                {
                    Dispatcher.Invoke(() =>
                    {
                        TerminalOutput.AppendText(new string(buffer, 0, read));
                        TerminalOutput.ScrollToEnd();
                    });
                }
            }
        }

        private async Task EnsureConnected()
        {
            if (!_sshClient.IsConnected)
            {
                try
                {
                    _sshClient.Connect();
                    StartShellStream();  // Reiniciar el ShellStream después de reconectar
                    StartResourceMonitoring();  // Reiniciar el monitoreo
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Error al reconectar: {ex.Message}");
                    });
                    await Task.Delay(5000);  // Espera 5 segundos antes de volver a intentar
                    await EnsureConnected();  // Reintenta la conexión
                }
            }
        }

        private void StartMinecraftServer(object sender, RoutedEventArgs e)
        {
            await EnsureConnected();  // Asegura que esté conectado

            // Verificar si ya existe una sesión de screen llamada "server"
            var checkScreenCommand = _sshClient.CreateCommand("screen -ls | grep server");
            var screenOutput = checkScreenCommand.Execute().Trim();

            if (string.IsNullOrEmpty(screenOutput))
            {
                // Si no existe, crear una nueva sesión screen y ejecutar el script start.sh
                var startServerCommand = _sshClient.CreateCommand("screen -dmS server ./start.sh");
                startServerCommand.Execute();
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("Servidor de Minecraft iniciado.");
                });
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("El servidor ya está en ejecución.");
                });
            }
        }


        private void StartResourceMonitoring()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    await EnsureConnected();  // Asegúrate de estar conectado antes de monitorear

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

                    await Task.Delay(5000);  // Espera 5 segundos antes de la siguiente actualización
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

    public class Credentials
    {
        public string ServerIp { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
