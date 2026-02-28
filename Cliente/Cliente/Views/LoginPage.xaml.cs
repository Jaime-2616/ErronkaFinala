using Cliente.Services;
using Cliente.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Cliente.Views
{
    public partial class LoginPage : Page
    {
        private readonly Action<string> _onLoginSuccess;
        private readonly LoginViewModel _vm;

        public LoginPage(Action<string> onLoginSuccess)
        {
            InitializeComponent();
            _onLoginSuccess = onLoginSuccess;

            // ViewModel usando AuthService real
            _vm = new LoginViewModel(new AuthService());
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string emailOrUser = txtEmail.Text.Trim();
            string pass = txtPassword.Password;

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

        private void BtnGoRegister_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService?.Navigate(new RegisterPage());
        }
    }
}
