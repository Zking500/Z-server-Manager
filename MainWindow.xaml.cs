using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Text.Json;
using Renci.SshNet;
using System.IO;


namespace Z_Server_Manager
{

    public partial class MainWindow : Window
    {
        internal const string CredentialsFilePath = "credentials.json";
        public class Credentials
        {
            public string ServerIp { get; set; }
            public int Port { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public bool RememberMe { get; set; }
        }

        public MainWindow()
        {
            InitializeComponent();
            LoadCredentials();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string serverIp = ServerIpTextBox.Text;
            int port = int.Parse(PortTextBox.Text);
            string username = UsernameTextBox.Text;
            string password = PasswordBox.Password;
            bool rememberMe = RememberMeCheckBox.IsChecked ?? false;

            try
            {
                using (var client = new SshClient(serverIp, port, username, password))
                {
                    client.Connect();
                    if (client.IsConnected)
                    {
                        MessageBox.Show("Login successful!");

                        if (rememberMe)
                        {
                            SaveCredentials(new Credentials
                            {
                                ServerIp = serverIp,
                                Port = port,
                                Username = username,
                                Password = password,
                                RememberMe = rememberMe
                            });
                        }
                        else
                        {
                            // Clear the saved credentials if Remember Me is unchecked
                            if (File.Exists(CredentialsFilePath))
                                File.Delete(CredentialsFilePath);
                        }

                        // Abre la ventana ListaServers después del inicio de sesión exitoso
                        var listaServersWindow = new ListaServers();
                        listaServersWindow.Show();

                        // Cierra la ventana principal si lo deseas
                        this.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Login failed: {ex.Message}");
            }
        }


        private void SaveCredentials(Credentials credentials)
        {
            var json = JsonSerializer.Serialize(credentials);
            File.WriteAllText(CredentialsFilePath, json);
        }

        private void LoadCredentials()
        {
            if (File.Exists(CredentialsFilePath))
            {
                var json = File.ReadAllText(CredentialsFilePath);
                var credentials = JsonSerializer.Deserialize<Credentials>(json);

                if (credentials != null && credentials.RememberMe)
                {
                    ServerIpTextBox.Text = credentials.ServerIp;
                    PortTextBox.Text = credentials.Port.ToString();
                    UsernameTextBox.Text = credentials.Username;
                    PasswordBox.Password = credentials.Password;
                    RememberMeCheckBox.IsChecked = credentials.RememberMe;
                }
            }
        }
    }
}
