using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Cliente.Models;

namespace Cliente.Views
{
    public partial class MovePickerWindow : Window
    {
        // Mugimendu guztiak eta uneko sloten mugimenduak
        private readonly IList<Move> _allMoves;
        private readonly IList<Move?> _currentSlots; // Pokemonaren 4 slotak
        private readonly int _slotIndex;             // Aukeratzen ari den slot-a (0..3)

        // Hautatutako mugimendua
        public Move? SelectedMove { get; private set; }

        // Leihoko konstruktorea, mugimenduak eta slotak jasotzen ditu
        public MovePickerWindow(
            IList<Move> allMoves,
            IList<Move?> currentSlots,
            int slotIndex)
        {
            InitializeComponent();
            _allMoves = allMoves;
            _currentSlots = currentSlots;
            _slotIndex = slotIndex;
            LoadMoves();
        }

        // Mugimenduak kargatzen ditu, erabilitakoak kenduz eta filtroa aplikatuz
        private void LoadMoves(string? filter = null)
        {
            var usedIds = _currentSlots
                .Where(m => m != null)
                .Select(m => m!.Id)
                .ToHashSet();

            // Slot honetan dagoen mugimendua ez da kendu
            int? currentId = _currentSlots[_slotIndex]?.Id;
            if (currentId.HasValue)
                usedIds.Remove(currentId.Value);

            var query = _allMoves
                .Where(m => !usedIds.Contains(m.Id));

            if (!string.IsNullOrWhiteSpace(filter))
                query = query.Where(m => m.Nombre.Contains(filter,
                               System.StringComparison.OrdinalIgnoreCase));

            ListMoves.ItemsSource = query
                .OrderBy(m => m.Nombre)
                .ToList();
        }

        // Bilaketa botoia klikatu denean filtroa aplikatzen du
        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            LoadMoves(TxtSearch.Text);
        }

        // Mugimendu bat doble klik eginez hautatzen da
        private void ListMoves_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ConfirmSelection();
        }

        // OK botoia klikatu denean hautatutako mugimendua baieztatzen du
        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            ConfirmSelection();
        }

        // Cancel botoia klikatu denean leihoa ixten du
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // Hautatutako mugimendua gordetzen du eta leihoa ixten du
        private void ConfirmSelection()
        {
            if (ListMoves.SelectedItem is Move move)
            {
                SelectedMove = move;
                DialogResult = true; // leihoa itxi eta deitzaileari jakinarazi
            }
        }
    }
}