using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using static Z_Server_Manager.MainWindow;

namespace Z_Server_Manager
{
    public partial class ListaServers : Window
    {
        private string _currentDirectory = "/home/"; // Directorio inicial
        private SshClient _sshClient;
        private string _baseDirectory = string.Empty;
        private Dictionary<string, BitmapImage> _iconCache = new Dictionary<string, BitmapImage>(); // Caché para iconos

        public ListaServers()
        {
            InitializeComponent();
            LoadServers(_currentDirectory);
        }

        private void BrowseServersButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement your logic to browse to the specific directory
        }

        private async void LoadServers(string directory)
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
                return;
            }

            try
            {
                // Conéctate usando las credenciales si no estás ya conectado
                if (_sshClient == null || !_sshClient.IsConnected)
                {
                    _sshClient = new SshClient(credentials.ServerIp, credentials.Port, credentials.Username, credentials.Password);
                    _sshClient.Connect();
                }

                // Conéctate también al servidor de archivos SFTP
                using (var sftpClient = new SftpClient(credentials.ServerIp, credentials.Port, credentials.Username, credentials.Password))
                {
                    sftpClient.Connect();

                    // Ejecuta el comando para listar los directorios en el directorio actual
                    var command = _sshClient.CreateCommand($"ls -F {directory} | grep '/$'");
                    var result = command.Execute();

                    // Divide el resultado en líneas
                    var serverNames = result.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    var serverList = new List<ServerItem>();

                    // Agrega un ítem especial "retroceder" al inicio de la lista
                    if (directory != "/")
                    {
                        serverList.Add(new ServerItem
                        {
                            ServerName = "..",
                            IconPath = null
                        });
                    }

                    // Itera sobre los nombres de los servidores
                    foreach (var serverName in serverNames)
                    {
                        var trimmedServerName = serverName.TrimEnd('/');
                        serverList.Add(new ServerItem
                        {
                            ServerName = trimmedServerName,
                            IconPath = null // Iconos se cargan asíncronamente más tarde
                        });
                    }

                    // Actualiza el ListView con los servidores encontrados
                    ServerListView.ItemsSource = serverList;

                    // Cargar iconos asíncronamente
                    await LoadServerIconsAsync(sftpClient, serverList, directory);
                }
            }
            catch (SshOperationTimeoutException ex)
            {
                MessageBox.Show("Error: No se tiene permiso para acceder a esta carpeta.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar servidores: {ex.Message}");
            }
        }

        private async Task LoadServerIconsAsync(SftpClient sftpClient, List<ServerItem> serverList, string directory)
        {
            foreach (var serverItem in serverList)
            {
                if (serverItem.ServerName == "..") continue; // No descargar icono para el ítem "retroceder"

                var remoteIconPath = Path.Combine(directory, serverItem.ServerName, "server-icon.png");

                if (_iconCache.ContainsKey(serverItem.ServerName))
                {
                    // Usar el icono almacenado en caché
                    serverItem.IconPath = _iconCache[serverItem.ServerName];
                    continue;
                }

                try
                {
                    if (sftpClient.Exists(remoteIconPath))
                    {
                        string localIconPath = Path.Combine(Path.GetTempPath(), $"{serverItem.ServerName}-server-icon.png");

                        // Descargar el icono de manera asíncrona
                        await Task.Run(() =>
                        {
                            using (var fileStream = new FileStream(localIconPath, FileMode.Create))
                            {
                                sftpClient.DownloadFile(remoteIconPath, fileStream);
                            }
                        });

                        var image = new BitmapImage(new Uri(localIconPath));
                        _iconCache[serverItem.ServerName] = image;

                        // Asignar el icono en la interfaz gráfica
                        Dispatcher.Invoke(() =>
                        {
                            serverItem.IconPath = image;
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al cargar el icono del servidor {serverItem.ServerName}: {ex.Message}");
                }
            }
        }

        private void ServerListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ServerListView.SelectedItem is ServerItem selectedServer)
            {
                // Cambia al directorio seleccionado
                _currentDirectory = Path.Combine(_currentDirectory, selectedServer.ServerName) + "/";

                // Si el servidor tiene icono, abre otra ventana (por ahora con mensaje de construcción)
                if (selectedServer.IconPath != null)
                {
                    // Aquí puedes abrir una nueva ventana (que todavía no has programado)
                    MessageBox.Show("Esta sección está en construcción.");
                    return;
                }

                // Vuelve a cargar el nuevo directorio
                LoadServers(_currentDirectory);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Navega hacia atrás en el directorio
            if (_currentDirectory != "/")
            {
                _currentDirectory = Path.GetDirectoryName(_currentDirectory.TrimEnd('/')) + "/";
                LoadServers(_currentDirectory);
            }
        }

        // Clase que representa un servidor en la lista
        public class ServerItem
        {
            public string ServerName { get; set; }
            public BitmapImage IconPath { get; set; }
        }
    }
}
