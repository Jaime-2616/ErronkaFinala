using Cliente.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Cliente.Views
{
    public partial class LoginPage : Page
    {
        // Login arrakastatsua gertatzean deitzen den ekintza
        private readonly Action<string> _onLoginSuccess;

        public LoginPage(Action<string> onLoginSuccess)
        {
            InitializeComponent();
            _onLoginSuccess = onLoginSuccess;
        }

        // Login botoia klikatu denean deitzen da
        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string emailOrUser = txtEmail.Text.Trim();
            string pass = txtPassword.Password;

            // Email edo pasahitza hutsik bada, mezua erakusten du
            if (string.IsNullOrEmpty(emailOrUser) || string.IsNullOrEmpty(pass))
            {
                MessageBox.Show("Please enter email and password.");
                return;
            }

            // Zerbitzarira login eskaera bidaltzen du
            string response = ServerService.SendRequest("login", emailOrUser, pass);

            // Errore mezua jasotzen bada, erakutsi eta irten
            if (!string.IsNullOrEmpty(response) && response.StartsWith("ERROR|"))
            {
                MessageBox.Show(response.Substring("ERROR|".Length));
                return;
            }

            // Login arrakastatsua: erabiltzaile izena ezarri
            ServerService.CurrentUsername = emailOrUser;

            // LobbyView orria sortu eta nabigatu
            var lobbyPage = new Page { Content = new LobbyView(emailOrUser) };

            _onLoginSuccess?.Invoke(emailOrUser);

            if (this.NavigationService != null)
            {
                this.NavigationService.Navigate(lobbyPage);
            }
            else
            {
                var main = Application.Current.MainWindow as MainWindow;
                main?.MainFrame.Navigate(lobbyPage);
            }
        }

        // Erregistratzeko orrira nabigatzeko botoia
        private void BtnGoRegister_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService?.Navigate(new RegisterPage());
        }
    }
}
