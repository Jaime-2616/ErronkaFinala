using Cliente.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Cliente.Views
{
    public partial class RegisterPage : Page
    {
        public RegisterPage()
        {
            InitializeComponent();
        }

        // Erregistratzeko botoia klikatu denean deitzen da
        private void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            string name = txtName.Text.Trim();
            string pass = txtPassword.Password;
            string passConfirm = txtPasswordConfirm.Password;

            // Izena eta pasahitza derrigorrezkoak dira
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(pass))
            {
                MessageBox.Show("El nombre y la contraseña son obligatorios.");
                return;
            }

            // Pasahitzak berdinak izan behar dira
            if (pass != passConfirm)
            {
                MessageBox.Show("Las contraseñas no coinciden.");
                return;
            }

            // Erregistro eskaera bidaltzen du zerbitzarira
            string response = ServerService.SendRequest("register", name, pass);

            // Errore mezua jasotzen bada, erakutsi eta irten
            if (!string.IsNullOrEmpty(response) && response.StartsWith("ERROR|"))
            {
                MessageBox.Show(response.Substring("ERROR|".Length));
                return;
            }

            // Erregistro arrakastatsua: mezua erakutsi eta login orrira nabigatu
            MessageBox.Show(response);
            this.NavigationService?.Navigate(new LoginPage(_ => { }));
        }

<<<<<<< HEAD
        // Login orrira joateko botoia
=======
        // Login orrira joateko botoi
>>>>>>> 4734c714d5d0921e55df30fbb4ea173a5a4c433
        private void BtnGoLogin_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService?.Navigate(new LoginPage(_ => { }));
        }
    }
}
