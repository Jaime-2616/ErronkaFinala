// Leiho berria code-behind (sinplea)
using System.Windows;
using Cliente.Models;
using System.Collections.Generic;
using System.Linq;
using Cliente.Services;
using System.Text.Json;
using System.Windows.Controls; // XAMLen erabiltzen da jada, hemen ere inportatzen dugu

namespace Cliente.Views
{
    public partial class TeamDetailsWindow : Window
    {
        // Taldearen izena eta bere Pokémonak
        private readonly string _teamName;
        private readonly List<Pokemon> _pokemons;

        // Leihoaren konstruktorea: taldearen izena eta Pokémonak jasotzen ditu
        public TeamDetailsWindow(string teamName, IEnumerable<Pokemon> pokemons)
        {
            InitializeComponent();

            _teamName = teamName;
            _pokemons = new List<Pokemon>(pokemons ?? new Pokemon[0]);

            // Taldearen izena erakutsi eta Pokémonen zerrenda lotu
            LblTitle.Text = teamName;
            Items.ItemsSource = _pokemons;

            if (_pokemons.Count > 0)
                Items.SelectedIndex = 0;
        }

        // Leihoa ixteko botoia
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Mugimendu-slot bateko "..." botoia klikatu denean
        private async void SelectMove_Click(object sender, RoutedEventArgs e)
        {
            if (Items.SelectedItem is not Pokemon selected)
            {
                MessageBox.Show("Lehenengo Pokémon bat hautatu.");
                return;
            }

            if (sender is not FrameworkElement fe || fe.Tag is not string tag || !int.TryParse(tag, out int slot))
                return;

            // Pokémonaren motak (CSV), motarik ez badago mugimendu guztiak itzultzen dira
            string typesCsv = selected.Type ?? string.Empty;

            // Zerbitzariari mugimenduak eskatu motaren arabera
            var moves = await MovesService.GetMovesByTypesAsync(typesCsv);
            if (moves.Count == 0)
            {
                MessageBox.Show("Ez da mugimendurik aurkitu Pokémon honentzat.");
                return;
            }

            // Pokémonak dituen egungo mugimenduen zerrenda, bikoizketak saihesteko
            var currentSlots = new List<Move?>
            {
                selected.Move1Id.HasValue ? new Move { Id = selected.Move1Id.Value, Nombre = selected.Move1 } : null,
                selected.Move2Id.HasValue ? new Move { Id = selected.Move2Id.Value, Nombre = selected.Move2 } : null,
                selected.Move3Id.HasValue ? new Move { Id = selected.Move3Id.Value, Nombre = selected.Move3 } : null,
                selected.Move4Id.HasValue ? new Move { Id = selected.Move4Id.Value, Nombre = selected.Move4 } : null
            };

            // slot = 1..4; MovePickerWindow-k 0..3 indizea erabiltzen du
            var dlg = new MovePickerWindow(moves, currentSlots, slot - 1)
            {
                Owner = this
            };

            // Mugimendua aukeratu eta slotari esleitzen zaio
            if (dlg.ShowDialog() == true && dlg.SelectedMove != null)
            {
                var move = dlg.SelectedMove;

                switch (slot)
                {
                    case 1:
                        selected.Move1 = move.Nombre;
                        selected.Move1Id = move.Id;
                        break;
                    case 2:
                        selected.Move2 = move.Nombre;
                        selected.Move2Id = move.Id;
                        break;
                    case 3:
                        selected.Move3 = move.Nombre;
                        selected.Move3Id = move.Id;
                        break;
                    case 4:
                        selected.Move4 = move.Nombre;
                        selected.Move4Id = move.Id;
                        break;
                }

                // Slot-eko TextBoxean erakutsi (suposatuz "..." botoia TextBox-aren ondoan dagoela)
                if (fe.Parent is Panel panel)
                {
                    var textBox = panel.Children.OfType<TextBox>().FirstOrDefault();
                    if (textBox != null)
                        textBox.Text = move.Nombre ?? string.Empty;
                }

                // Zerrendako binding-ak freskatu
                Items.Items.Refresh();
            }
        }

        // Taldearen mugimenduak datu-basean gordetzeko botoia
        private async void SaveMoves_Click(object sender, RoutedEventArgs e)
        {
            if (_pokemons.Count == 0)
            {
                MessageBox.Show("Ez dago Pokémonik talde honetan.");
                return;
            }

            // Mugimenduak JSON formatuan prestatzen ditu
            var payload = new
            {
                TeamName = _teamName,
                Pokemons = _pokemons.Select(p => new
                {
                    PokemonId = p.Id,
                    Move1Id = p.Move1Id,
                    Move2Id = p.Move2Id,
                    Move3Id = p.Move3Id,
                    Move4Id = p.Move4Id
                }).ToArray()
            };

            string json = JsonSerializer.Serialize(payload);

            try
            {
                // Mugimenduak zerbitzarira bidaltzen ditu eta emaitza erakusten du
                string resp = await System.Threading.Tasks.Task.Run(() =>
                    ServerService.SendRequest("save_team_moves", _teamName, json));

                if (resp.StartsWith("OK|"))
                {
                    MessageBox.Show("Mugimenduak behar bezala gorde dira.");
                }
                else
                {
                    MessageBox.Show("Errorea mugimenduak gordetzean: " + resp);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show("Errorea mugimenduak gordetzean: " + ex.Message);
            }
        }
    }
}