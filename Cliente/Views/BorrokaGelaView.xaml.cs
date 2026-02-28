using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Cliente.Models;

namespace Cliente.Views
{
    public partial class BorrokaGelaView : UserControl
    {
        // Jokalarien eta aurkariaren Pokemon taldeak
        public ObservableCollection<Pokemon> PlayerTeam { get; } = new();
        public ObservableCollection<Pokemon> RivalTeam { get; } = new();

        // Mugimendua aukeratzean ekitaldia (slot 1..4)
        public event Action<int>? MoveSelected;

        // Aukeratutako Pokemon-a (jokalaria)
        public static readonly DependencyProperty SelectedPokemonProperty =
            DependencyProperty.Register(
                nameof(SelectedPokemon),
                typeof(Pokemon),
                typeof(BorrokaGelaView),
                new PropertyMetadata(null));

        public Pokemon? SelectedPokemon
        {
            get => (Pokemon?)GetValue(SelectedPokemonProperty);
            private set => SetValue(SelectedPokemonProperty, value);
        }

        // Aukeratutako Pokemon-a (aurkaria)
        public static readonly DependencyProperty RivalSelectedPokemonProperty =
            DependencyProperty.Register(
                nameof(RivalSelectedPokemon),
                typeof(Pokemon),
                typeof(BorrokaGelaView),
                new PropertyMetadata(null));

        public Pokemon? RivalSelectedPokemon
        {
            get => (Pokemon?)GetValue(RivalSelectedPokemonProperty);
            private set => SetValue(RivalSelectedPokemonProperty, value);
        }

        // Mugimendu botoiak aktibatzeko/desaktibatzeko propietatea
        public static readonly DependencyProperty AreMovesEnabledProperty =
            DependencyProperty.Register(
                nameof(AreMovesEnabled),
                typeof(bool),
                typeof(BorrokaGelaView),
                new PropertyMetadata(true));

        public bool AreMovesEnabled
        {
            get => (bool)GetValue(AreMovesEnabledProperty);
            set => SetValue(AreMovesEnabledProperty, value);
        }

        public BorrokaGelaView()
        {
            InitializeComponent();
            DataContext = this;
        }

        // Jokalarien taldea ezartzen du eta lehenengo bizirik dagoen Pokemon-a aukeratzen du
        public void SetPlayerTeam(Pokemon[] pokemons)
        {
            PlayerTeam.Clear();
            foreach (var p in pokemons)
                PlayerTeam.Add(p);

            SelectedPokemon = PlayerTeam.FirstOrDefault(p => (p.HP ?? 0) > 0);
        }

        // Aurkariaren taldea ezartzen du eta lehenengo bizirik dagoen Pokemon-a aukeratzen du
        public void SetRivalTeam(Pokemon[] pokemons)
        {
            RivalTeam.Clear();
            foreach (var p in pokemons)
                RivalTeam.Add(p);

            RivalSelectedPokemon = RivalTeam.FirstOrDefault(p => (p.HP ?? 0) > 0);
        }

        // Mugimendu bakoitzerako botoiaren ekintza
        private void Move1_Click(object sender, RoutedEventArgs e) => MoveSelected?.Invoke(1);
        private void Move2_Click(object sender, RoutedEventArgs e) => MoveSelected?.Invoke(2);
        private void Move3_Click(object sender, RoutedEventArgs e) => MoveSelected?.Invoke(3);
        private void Move4_Click(object sender, RoutedEventArgs e) => MoveSelected?.Invoke(4);

        // Aukeratutako Pokemon-en bindingak eguneratzeko
        public void RefreshActives()
        {
            var currentSelected = SelectedPokemon;
            SelectedPokemon = null;
            SelectedPokemon = currentSelected;

            var currentRival = RivalSelectedPokemon;
            RivalSelectedPokemon = null;
            RivalSelectedPokemon = currentRival;
        }
    }
}