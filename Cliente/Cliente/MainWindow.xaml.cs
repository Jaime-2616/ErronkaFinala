using Cliente.Services;
using Cliente.Views;
using System.Windows;

namespace Cliente
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Página inicial: Login
            MainFrame.Navigate(new LoginPage(OnLoginSuccess));
        }

        // Este callback se llamará desde LoginPage cuando el login sea OK
        private void OnLoginSuccess(string username)
        {
            // Asegurar que CurrentUsername se rellena UNA sola vez aquí
            ServerService.CurrentUsername = username;

            // Navegar a la página principal que contiene TeamsPage, etc.
            MainFrame.Navigate(new TeamsPage());
        }
    }
}
