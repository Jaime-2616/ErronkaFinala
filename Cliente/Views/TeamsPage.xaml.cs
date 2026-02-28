using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Cliente.Models;
using Cliente.Services;
using System.Text.Json;
using System.Windows.Input;

namespace Cliente.Views
{
    public partial class TeamsPage : UserControl
    {
        // Pokemonen zerrenda eta ikuspegi filtratua
        ObservableCollection<Pokemon> Pokemons { get; } = new();
        ICollectionView? PokemonsView;

        // Taldeen izenak gordetzen dituen zerrenda
        ObservableCollection<string> Teams { get; } = new();

        // Uneko taldea (gehienez 6 Pokemon)
        ObservableCollection<Pokemon> CurrentTeam { get; } = new();

        public TeamsPage()
        {
            InitializeComponent();

            // Pokemonen ikuspegia filtratzeko prestatu
            PokemonsView = CollectionViewSource.GetDefaultView(Pokemons);
            cmbPokemons.ItemsSource = PokemonsView;

            // wire search handler
            txtSearch.TextChanged += (s, e) => ApplyFilter();

            // wire teams list
            TeamsListBox.ItemsSource = Teams;

            // bind current team list
            CurrentTeamListBox.ItemsSource = CurrentTeam;

            // handle double-click on team -> open details window
            TeamsListBox.MouseDoubleClick += TeamsListBox_MouseDoubleClick;

            // navegación entre vistas
            BtnViewA.Click += (s, e) =>
            {
                ViewA.Visibility = Visibility.Visible;
                ViewB.Visibility = Visibility.Collapsed;
            };
            BtnViewB.Click += (s, e) =>
            {
                ViewA.Visibility = Visibility.Collapsed;
                ViewB.Visibility = Visibility.Visible;
            };

            // Talde bat ezabatzeko botoia
            BtnDeleteTeam.Click += BtnDeleteTeam_Click;

            // Pokemona eta taldeak asinkronoki kargatu
            _ = LoadPokemonsAsync();
            _ = LoadTeamsAsync();
        }

        // Pokemona asinkronoki kargatzen du zerbitzaritik
        async Task LoadPokemonsAsync()
        {
            try
            {
                var list = await PokedexService.GetPokemonsAsync();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Pokemons.Clear();
                    foreach (var p in list)
                        Pokemons.Add(p);
                });
            }
            catch
            {
                // isilik, zerrenda hutsik mantentzen da
            }
        }

        // Taldeen izenak kargatzen ditu zerbitzaritik
        async Task LoadTeamsAsync()
        {
            try
            {
                string user = ServerService.CurrentUsername ?? string.Empty;
                string resp = await Task.Run(() => ServerService.SendRequest("get_teams", user, ""));
                if (string.IsNullOrEmpty(resp))
                {
                    SetNoTeams();
                    return;
                }

                if (resp.StartsWith("OK|"))
                {
                    string json = resp.Substring(3);
                    try
                    {
                        var names = JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Teams.Clear();
                            if (names.Length == 0)
                            {
                                Teams.Add("no hay equipos creados");
                            }
                            else
                            {
                                foreach (var n in names)
                                    Teams.Add(string.IsNullOrWhiteSpace(n) ? "(sin nombre)" : n);
                            }
                        });
                        return;
                    }
                    catch
                    {
                        // errorean, ez da talderik gehitzen
                    }
                }

                SetNoTeams();
            }
            catch
            {
                SetNoTeams();
            }
        }

        // Talderik ez dagoenean, mezu bat gehitzen du
        void SetNoTeams()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Teams.Clear();
                Teams.Add("no hay equipos creados");
            });
        }

        // Pokemona filtratzeko logika
        void ApplyFilter()
        {
            if (PokemonsView == null) return;
            string q = txtSearch.Text?.Trim() ?? "";

            const string placeholder = "Buscar Pokmon...";
            if (string.IsNullOrEmpty(q) || string.Equals(q, placeholder, StringComparison.OrdinalIgnoreCase))
            {
                PokemonsView.Filter = null;
            }
            else
            {
                PokemonsView.Filter = o =>
                {
                    if (o is Pokemon pk)
                    {
                        return pk.Name != null && pk.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    return false;
                };
            }
            PokemonsView.Refresh();
        }

        // Bilaketa testua hutsik uzten denean placeholder-a jartzen du
        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null && string.IsNullOrWhiteSpace(textBox.Text))
            {
                textBox.Text = "Buscar Pokmon...";
                textBox.Foreground = (System.Windows.Media.Brush)this.FindResource("MutedBrush");
            }
        }

        // Bilaketa testua fokua hartzen duenean placeholder-a kentzen du
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null && textBox.Text == "Buscar Pokmon...")
            {
                textBox.Text = "";
                textBox.Foreground = System.Windows.Media.Brushes.Black;
            }
        }

        // ====== Taldearen kudeaketa logika ======

        // Pokemona taldera gehitzeko botoia
        private void BtnAddPokemon_Click(object sender, RoutedEventArgs e)
        {
            if (cmbPokemons.SelectedItem is not Pokemon selected)
            {
                MessageBox.Show("Selecciona un Pokmon primero.");
                return;
            }

            if (CurrentTeam.Count >= 6)
            {
                MessageBox.Show("El equipo ya tiene 6 Pokmon.");
                return;
            }

            // Ez du uzten errepikatutako Pokemonak taldera gehitzen
             if (CurrentTeam.Any(p => p.Id == selected.Id))
            {
               MessageBox.Show("Ese Pokmon ya est en el equipo.");
                return;
             }

            CurrentTeam.Add(selected);
        }

        // Taldea garbitzeko botoia
        private void BtnClearTeam_Click(object sender, RoutedEventArgs e)
        {
            txtTeamName.Text = string.Empty;
            CurrentTeam.Clear();
        }

        // Taldea gordetzeko botoia, erabiltzailearekin batera
        private async void BtnSaveTeam_Click(object sender, RoutedEventArgs e)
        {
            string teamName = txtTeamName.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(teamName))
            {
                MessageBox.Show("Introduce un nombre para el equipo.");
                return;
            }

            if (CurrentTeam.Count == 0)
            {
                MessageBox.Show("El equipo debe tener al menos un Pokmon.");
                return;
            }

            if (CurrentTeam.Count > 6)
            {
                MessageBox.Show("El equipo no puede tener ms de 6 Pokmon.");
                return;
            }

            var ids = CurrentTeam.Select(p => p.Id).ToArray();
            string jsonIds = JsonSerializer.Serialize(ids);

            try
            {
                string user = ServerService.CurrentUsername ?? string.Empty;

                string p2 = $"{teamName}|{jsonIds}";
                string resp = ServerService.SendRequest("create_team", user, p2);

                if (resp.StartsWith("OK|"))
                {
                    MessageBox.Show("Equipo guardado correctamente.");
                    CurrentTeam.Clear();
                    txtTeamName.Text = string.Empty;
                    await LoadTeamsAsync();
                }
                else
                {
                    MessageBox.Show(resp);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar el equipo: " + ex.Message);
            }
        }

        // Pokemona taldetik kentzeko botoia
        private void RemovePokemon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Pokemon pk)
            {
                CurrentTeam.Remove(pk);
            }
        }

        // Hautatutako Pokemona taldetik kentzeko botoia
        private void RemoveSelectedPokemon_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentTeamListBox.SelectedItem is Pokemon pk)
            {
                CurrentTeam.Remove(pk);
            }
        }

        // Taldean doble klik eginez, bere Pokémonak erakusten dituen leihoa irekitzen du
        private async void TeamsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TeamsListBox.SelectedItem is not string teamName) return;
            if (string.IsNullOrWhiteSpace(teamName)) return;
            if (teamName == "no hay equipos creados") return;

            try
            {
                var poks = await LoadTeamPokemonsAsync(teamName);
                var wnd = new TeamDetailsWindow(teamName, poks);
                wnd.Owner = Window.GetWindow(this);
                wnd.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al obtener los pokémon del equipo: " + ex.Message);
            }
        }

        // Talde baten Pokémonak zerbitzaritik kargatzen ditu
        async Task<Pokemon[]> LoadTeamPokemonsAsync(string teamName)
        {
            return await Task.Run(() =>
            {
                string resp = ServerService.SendRequest("get_team", teamName ?? "", "");
                if (string.IsNullOrEmpty(resp)) return Array.Empty<Pokemon>();
                if (!resp.StartsWith("OK|")) return Array.Empty<Pokemon>();

                string json = resp.Substring(3);
                try
                {
                    var list = JsonSerializer.Deserialize<Pokemon[]>(json);
                    if (list == null) return Array.Empty<Pokemon>();
                    return list.Take(6).ToArray();
                }
                catch
                {
                    return Array.Empty<Pokemon>();
                }
            });
        }

        // Talde bat ezabatzeko botoia eta logika
        private async void BtnDeleteTeam_Click(object sender, RoutedEventArgs e)
        {
            if (TeamsListBox.SelectedItem is not string teamName ||
                string.IsNullOrWhiteSpace(teamName) ||
                teamName == "no hay equipos creados")
            {
                MessageBox.Show("Selecciona un equipo válido para eliminar.");
                return;
            }

            var confirm = MessageBox.Show(
                $"¿Seguro que quieres eliminar el equipo \"{teamName}\"?",
                "Eliminar equipo",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                string user = ServerService.CurrentUsername ?? string.Empty;

                string resp = await Task.Run(() =>
                    ServerService.SendRequest("delete_team", user, teamName));

                if (resp.StartsWith("OK|"))
                {
                    MessageBox.Show("Equipo eliminado correctamente.");
                    await LoadTeamsAsync();
                }
                else
                {
                    MessageBox.Show("Error al eliminar el equipo: " + resp);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al eliminar el equipo: " + ex.Message);
            }
        }

        // Lobby-ra itzultzeko botoia
        private void BtnBackToLobby_Click(object sender, RoutedEventArgs e)
        {
            var lobby = new LobbyView(ServerService.CurrentUsername ?? string.Empty);

            var parentPage = this.FindParentPage();
            if (parentPage?.NavigationService != null)
            {
                parentPage.NavigationService.Navigate(lobby);
                return;
            }

            var main = Application.Current.MainWindow as MainWindow;
            main?.MainFrame.Navigate(lobby);
        }
    }
}
