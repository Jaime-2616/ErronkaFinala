using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace Cliente.Views
{
    public partial class TeamSelectWindow : Window
    {
        // Hautatutako taldearen izena
        public string? SelectedTeamName { get; private set; }

        // Leihoaren konstruktorea: jokalariaren izena eta talde izenak jasotzen ditu
        public TeamSelectWindow(string playerName, string[] teamNames)
        {
            InitializeComponent();

            Title = $"Seleccionar equipo - {playerName}";
            txtPlayerName.Text = playerName;
            lstTeams.ItemsSource = new ObservableCollection<string>(teamNames);
        }

        // Onartu botoia klikatu denean, hautatutako taldea gordetzen du eta leihoa ixten du
        private void BtnAccept_Click(object sender, RoutedEventArgs e)
        {
            if (lstTeams.SelectedItem is string name && !string.IsNullOrWhiteSpace(name))
            {
                SelectedTeamName = name;
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("Selecciona un equipo.");
            }
        }

        // Bertan behera botoia klikatu denean, leihoa ixten du
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}