using Cliente.Services;
<<<<<<< HEAD
using Cliente.ViewModels;
=======
>>>>>>> 4734c714d5d0921e55df30fbb4ea173a5a4c4332
using System;
using System.Windows;
using System.Windows.Controls;

namespace Cliente.Views
{
    public partial class LoginPage : Page
    {
<<<<<<< HEAD
        private readonly Action<string> _onLoginSuccess;
        private readonly LoginViewModel _vm;
=======
        // Login arrakastatsua gertatzean deitzen den ekintza
        private readonly Action<string> _onLoginSuccess;
>>>>>>> 4734c714d5d0921e55df30fbb4ea173a5a4c4332

        public LoginPage(Action<string> onLoginSuccess)
        {
            InitializeComponent();
            _onLoginSuccess = onLoginSuccess;
<<<<<<< HEAD

            // ViewModel usando AuthService real
            _vm = new LoginViewModel(new AuthService());
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
=======
        }

        // Login botoia klikatu denean deitzen da
        private void BtnLogin_Click(object sender, RoutedEventArgs e)
>>>>>>> 4734c714d5d0921e55df30fbb4ea173a5a4c4332
        {
            string emailOrUser = txtEmail.Text.Trim();
            string pass = txtPassword.Password;

<<<<<<< HEAD
            _vm.Username = emailOrUser;
            _vm.Password = pass;

            bool ok = await _vm.LoginAsync();
            if (!ok)
            {
                MessageBox.Show(_vm.ErrorMessage);
                return;
            }

            // Login correcto: sincronizamos con tu ServerService actual
            ServerService.CurrentUsername = emailOrUser;

=======
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
>>>>>>> 4734c714d5d0921e55df30fbb4ea173a5a4c4332
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

<<<<<<< HEAD
=======
        // Erregistratzeko orrira nabigatzeko botoia
>>>>>>> 4734c714d5d0921e55df30fbb4ea173a5a4c4332
        private void BtnGoRegister_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService?.Navigate(new RegisterPage());
        }
    }
}
