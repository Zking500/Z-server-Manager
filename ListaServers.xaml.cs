using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using static Z_Server_Manager.MainWindow;

namespace Z_Server_Manager
{
    public partial class ListaServers : Window
    {
        private string _currentDirectory = "/home/"; // Directorio inicial
        private SshClient _sshClient;
        private bool _isServerSelected = false; // Variable para controlar la selección de servidor

        public ListaServers()
        {
            InitializeComponent();
            LoadServers(_currentDirectory);
        }

        private void SeleccionarServerButton_Click(object sender, RoutedEventArgs e)
        {
            // Al hacer clic en el botón, cambia la variable a true o false
            _isServerSelected = !_isServerSelected; // Alterna el valor
            if (_isServerSelected)
            {
                MessageBox.Show("Modo de selección activado. El siguiente servidor que selecciones se guardará.");
            }
            else
            {
                MessageBox.Show("Modo de selección desactivado. Puedes seguir navegando.");
            }
        }

        private void LoadServers(string directory)
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
                            ServerName = ".."
                        });
                    }

                    // Itera sobre los nombres de los servidores
                    foreach (var serverName in serverNames)
                    {
                        var trimmedServerName = serverName.TrimEnd('/');
                        serverList.Add(new ServerItem
                        {
                            ServerName = trimmedServerName
                        });
                    }

                    // Actualiza el ListView con los servidores encontrados
                    ServerListView.ItemsSource = serverList;
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

        private void ServerListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ServerListView.SelectedItem is ServerItem selectedServer)
            {
                // Cambia al directorio seleccionado
                _currentDirectory = Path.Combine(_currentDirectory, selectedServer.ServerName) + "/";

                // Si el servidor está seleccionado, guarda su dirección
                if (_isServerSelected && selectedServer.ServerName != "..")
                {
                    SaveSelectedServer(_currentDirectory);
                    MessageBox.Show("Servidor seleccionado: " + _currentDirectory);
                    ServidorControler ServidorControler = new ServidorControler();
                    ServidorControler.Show();
                    this.Close(); // Cierra la ventana actual
                }
                else
                {
                    // Vuelve a cargar el nuevo directorio
                    LoadServers(_currentDirectory);
                }
            }
        }

        private void SaveSelectedServer(string selectedDirectory)
        {
            var serverAddress = new { Directory = selectedDirectory };
            var json = JsonSerializer.Serialize(serverAddress);
            File.WriteAllText("DireccionServer.json", json);

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
        }
    }
}
