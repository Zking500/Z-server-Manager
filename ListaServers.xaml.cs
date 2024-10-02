using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using static Z_Server_Manager.MainWindow;

namespace Z_Server_Manager
{
    public partial class ListaServers : Window
    {
        private string _currentDirectory = "/"; // Directorio inicial
        private SshClient _sshClient;
        private string _baseDirectory = string.Empty;

        public ListaServers()
        {
            InitializeComponent();
        }

        private void BrowseServersButton_Click(object sender, RoutedEventArgs e)
        {
            // Abre el diálogo para seleccionar la carpeta base que contiene los servidores
            var folderDialog = new System.Windows.Forms.FolderBrowserDialog();
            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _baseDirectory = folderDialog.SelectedPath;
                LoadServers(_currentDirectory);
            }
        }

        private void LoadServers(string directory)
        {
            // Limpia la lista de servidores actuales
            ServerListView.Items.Clear();

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

            // Conéctate usando las credenciales si no estás ya conectado
            if (_sshClient == null || !_sshClient.IsConnected)
            {
                _sshClient = new SshClient(credentials.ServerIp, credentials.Port, credentials.Username, credentials.Password);
                _sshClient.Connect();
            }

            try
            {
                // Ejecuta el comando para listar los directorios en el directorio actual
                var command = _sshClient.CreateCommand($"ls -F {directory} | grep '/$'"); // Comando para listar solo carpetas
                var result = command.Execute();

                // Divide el resultado en líneas
                var serverNames = result.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var serverList = new List<ServerItem>();

                foreach (var serverName in serverNames)
                {
                    var trimmedServerName = serverName.TrimEnd('/'); // Elimina la barra final '/'
                    var iconPath = Path.Combine(_baseDirectory, trimmedServerName, "server-icon.png");

                    // Verifica si existe el icono de server-icon.png
                    if (File.Exists(iconPath))
                    {
                        serverList.Add(new ServerItem
                        {
                            ServerName = trimmedServerName,
                            IconPath = new BitmapImage(new Uri(iconPath))
                        });
                    }
                    else
                    {
                        // Si no hay icono, agregamos solo el nombre del servidor
                        serverList.Add(new ServerItem
                        {
                            ServerName = trimmedServerName,
                            IconPath = null
                        });
                    }
                }

                // Actualiza el ListView con los servidores encontrados
                ServerListView.ItemsSource = serverList;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar servidores: {ex.Message}");
            }
        }

        private void ServerListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ServerListView.SelectedItem is ServerItem selectedServer)
            {
                // Cambia al directorio seleccionado
                _currentDirectory = Path.Combine(_currentDirectory, selectedServer.ServerName) + "/";

                // Vuelve a cargar el nuevo directorio
                LoadServers(_currentDirectory);
            }
        }

        // Clase que representa un servidor en la lista
        public class ServerItem
        {
            public string ServerName { get; set; }
            public BitmapImage IconPath { get; set; }
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
    }
}
