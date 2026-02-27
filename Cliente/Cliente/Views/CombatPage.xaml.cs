using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Cliente.Models;
using Cliente.Services;

namespace Cliente.Views
{
    public partial class CombatPage : Page
    {
        // Jokalari bakoitzaren izena eta hautatutako taldea
        private string _player1 = "";
        private string _player2 = "";

        public string? Player1SelectedTeam { get; private set; }
        public string? Player2SelectedTeam { get; private set; }

        // ChatService instantzia, borroka komunikaziorako
        private readonly ChatService? _chatService;

        // Borroka motorea eta taldeak
        private BattleEngine? _battle;
        private Pokemon[] _myTeam = Array.Empty<Pokemon>();
        private Pokemon[] _rivalTeam = Array.Empty<Pokemon>();

        // Mugimenduak aukeratzeko zain dauden slot-ak
        private int? _pendingMyMoveSlot;
        private int? _pendingRivalMoveSlot;

        // Txanda zenbakia
        private int _turnNumber = 0;

        // Borroka amaieraren mezua ez bidaltzeko bandera
        private bool _battleEndSent = false;

        // 🔹 WPFrako beharrezko konstruktore hutsa
        public CombatPage()
        {
            InitializeComponent();
        }

        // 🔹 Jokalariak dituen konstruktorea
        public CombatPage(string player1, string player2) : this()
        {
            _player1 = player1;
            _player2 = player2;
            PlayersVsText.Text = $"{_player1} vs {_player2}";

            Loaded += CombatPage_Loaded;

            // Mugimendu botoiak klikatu direnean kudeatu
            BattleView.MoveSelected += OnLocalMoveSelected;

            var user = ServerService.CurrentUsername;
            if (!string.IsNullOrWhiteSpace(user))
            {
                _chatService = new ChatService(user);

                // Aurkariak taldea hautatzen duenean, kargatu eta erakutsi
                _chatService.RivalTeamSelected += async (rivalUser, teamName) =>
                {
                    var currentUser = ServerService.CurrentUsername ?? "";
                    var expectedRival = string.Equals(currentUser, _player1, StringComparison.OrdinalIgnoreCase) ? _player2 : _player1;
                    if (!string.Equals(rivalUser, expectedRival, StringComparison.OrdinalIgnoreCase)) return;

                    await Dispatcher.InvokeAsync(async () => await LoadAndShowRivalTeamAsync(teamName));
                };

                // Aurkariak amore ematen badu, lobby-ra itzuli
                _chatService.SurrenderReceived += fromUser =>
                {
                    var currentUser = ServerService.CurrentUsername ?? "";
                    var expectedRival = string.Equals(currentUser, _player1, StringComparison.OrdinalIgnoreCase) ? _player2 : _player1;
                    if (!string.Equals(fromUser, expectedRival, StringComparison.OrdinalIgnoreCase)) return;

                    Dispatcher.Invoke(VolverALobby);
                };

                // Aurkariaren mugimendua jasotzen duenean
                _chatService.RivalMoveSelected += (fromUser, slot) =>
                {
                    var currentUser = ServerService.CurrentUsername ?? "";
                    var expectedRival = string.Equals(currentUser, _player1, StringComparison.OrdinalIgnoreCase) ? _player2 : _player1;
                    if (!string.Equals(fromUser, expectedRival, StringComparison.OrdinalIgnoreCase)) return;

                    Dispatcher.Invoke(() =>
                    {
                        _pendingRivalMoveSlot = slot;
                        TryResolveTurnIfReady();
                    });
                };

                // Borroka amaitzen denean, mugimenduak desgaitu eta lobby-ra itzuli
                _chatService.BattleEnded += winner =>
                {
                    Dispatcher.InvokeAsync(async () =>
                    {
                        BattleView.AreMovesEnabled = false;

                        AppendLog($"*** \"{winner}\" irabazi du ***");
                        AppendLog("5 segundo barru lobby-ra itzultzen...");

                        await Task.Delay(TimeSpan.FromSeconds(5));

                        VolverALobby();
                    });
                };

                _chatService.Start();
            }
        }

        // Mugimendu bat aukeratzen denean deitzen da
        private void OnLocalMoveSelected(int slot)
        {
            if (_battle == null) return;
            if (_pendingMyMoveSlot.HasValue) return;

            // Aktibo dagoen Pokémonik ez badago, ez du onartzen
            if (BattleView.SelectedPokemon == null || (BattleView.SelectedPokemon.HP ?? 0) <= 0)
                return;

            _pendingMyMoveSlot = slot;

            // Mugimendu botoiak desgaitu txanda ebazten den arte
            BattleView.AreMovesEnabled = false;
            AppendLog($"Txanda {_turnNumber + 1}: aurkaria itxaroten...");

            var currentUser = ServerService.CurrentUsername ?? "";
            var rivalUser = string.Equals(currentUser, _player1, StringComparison.OrdinalIgnoreCase) ? _player2 : _player1;

            _chatService?.SendMoveSelected(rivalUser, slot);

            TryResolveTurnIfReady();
        }

        // Bi jokalariek mugimendua aukeratu dutenean txanda ebazten du
        private void TryResolveTurnIfReady()
        {
            if (_battle == null) return;
            if (!_pendingMyMoveSlot.HasValue || !_pendingRivalMoveSlot.HasValue) return;

            int mySlot = _pendingMyMoveSlot.Value;
            int rivalSlot = _pendingRivalMoveSlot.Value;

            _pendingMyMoveSlot = null;
            _pendingRivalMoveSlot = null;

            PlayOneTurn(mySlot, rivalSlot);

            // Borroka bukatu ez bada, mugimenduak berriro aktibatu
            if (_battle != null && !_battle.IsFinished)
                BattleView.AreMovesEnabled = true;
        }

        // Orria kargatzean, jokalariak taldea aukeratu behar du
        private async void CombatPage_Loaded(object sender, RoutedEventArgs e)
        {
            var currentUser = ServerService.CurrentUsername;
            if (string.IsNullOrWhiteSpace(currentUser))
            {
                MessageBox.Show("Ez dago erabiltzaile saioa hasteko. Borroka bertan behera gelditzen da.");
                VolverALobby();
                return;
            }

            // Jokalariak bere taldea aukeratzen du
            string? myTeamName = await SelectTeamForPlayerAsync(currentUser);
            if (string.IsNullOrEmpty(myTeamName))
            {
                MessageBox.Show($"{currentUser} ez du taldeik hautatu. Borroka bertan behera gelditzen da.");
                VolverALobby();
                return;
            }

            if (string.Equals(currentUser, _player1, StringComparison.OrdinalIgnoreCase))
                Player1SelectedTeam = myTeamName;
            else if (string.Equals(currentUser, _player2, StringComparison.OrdinalIgnoreCase))
                Player2SelectedTeam = myTeamName;
            else
            {
                MessageBox.Show("Uneko erabiltzailea ez dator bat borrokan dauden jokalariekin. Borroka bertan behera gelditzen da.");
                VolverALobby();
                return;
            }

            // Hautatutako taldea kargatu eta erakutsi
            _myTeam = await LoadTeamPokemonsAsync(myTeamName);
            InitializeMaxHp(_myTeam);
            BattleView.SetPlayerTeam(_myTeam);

            // Zerbitzariari jakinarazi nire hautaketa
            _chatService?.SendTeamSelected(
                currentUser,
                string.Equals(currentUser, _player1, StringComparison.OrdinalIgnoreCase) ? _player2 : _player1,
                myTeamName);
        }

        // Aurkariaren taldea kargatu eta erakusten du
        private async Task LoadAndShowRivalTeamAsync(string rivalTeamName)
        {
            _rivalTeam = await LoadTeamPokemonsAsync(rivalTeamName);
            InitializeMaxHp(_rivalTeam);
            BattleView.SetRivalTeam(_rivalTeam);

            TryStartBattle();
        }

        // Bi taldeak prest daudenean borroka hasten du
        private void TryStartBattle()
        {
            if (_battle != null) return;
            if (_myTeam.Length == 0 || _rivalTeam.Length == 0) return;

            var currentUser = ServerService.CurrentUsername ?? "";
            bool iAmPlayer1 = string.Equals(currentUser, _player1, StringComparison.OrdinalIgnoreCase);

            var teamA = iAmPlayer1 ? _myTeam : _rivalTeam;
            var teamB = iAmPlayer1 ? _rivalTeam : _myTeam;

            _battle = new BattleEngine(teamA, teamB);

            // Lehen txandarako mugimenduak aktibatu
            BattleView.AreMovesEnabled = true;
            AppendLog("Borroka prest. Aukeratu mugimendu bat 1. txandarako.");
        }

        // Borroka log-ean testua gehitzen du
        private void AppendLog(string text)
        {
            if (BattleLogTextBox == null) return;

            if (!string.IsNullOrWhiteSpace(BattleLogTextBox.Text))
                BattleLogTextBox.AppendText(Environment.NewLine);

            BattleLogTextBox.AppendText(text + Environment.NewLine);
            BattleLogTextBox.ScrollToEnd();
        }

        // Txanda bat jokatzen du eta emaitzak erakusten ditu
        private void PlayOneTurn(int moveSlotMe, int moveSlotRival)
        {
            if (_battle == null) return;

            _turnNumber++;

            var currentUser = ServerService.CurrentUsername ?? "";
            bool iAmPlayer1 = string.Equals(currentUser, _player1, StringComparison.OrdinalIgnoreCase);

            string playerAName = _player1;
            string playerBName = _player2;

            int moveSlotA = iAmPlayer1 ? moveSlotMe : moveSlotRival;
            int moveSlotB = iAmPlayer1 ? moveSlotRival : moveSlotMe;

            var result = _battle.PlayTurn(playerAName, playerBName, moveSlotA, moveSlotB);

            // Taldeen egoera eguneratu
            if (iAmPlayer1)
            {
                _myTeam = _battle.TeamA;
                _rivalTeam = _battle.TeamB;
            }
            else
            {
                _myTeam = _battle.TeamB;
                _rivalTeam = _battle.TeamA;
            }

            AppendLog($"--- TXANDA {_turnNumber} ---");
            AppendLog(result.Info);

            foreach (var a in result.Actions)
            {
                AppendLog($"{a.AttackerPlayer} ({a.AttackerPokemon}) erabiltzen du {a.MoveName} (-{a.MovePower}) -> " +
                          $"{a.DefenderPlayer} ({a.DefenderPokemon}) HP={a.DefenderHpAfter}" +
                          (a.DefenderFainted ? " [AHULDUTA]" : ""));
            }

            // Ikuspegia eguneratu
            BattleView.SetPlayerTeam(_myTeam);
            BattleView.SetRivalTeam(_rivalTeam);

            var winner = _battle.WinnerName(_player1, _player2);
            if (winner != null)
            {
                AppendLog($"*** \"{winner}\" irabazi du ***");

                if (!_battleEndSent)
                {
                    _battleEndSent = true;
                    AppendLog($"*** \"{winner}\" irabazi du *** (zerbitzarira bidaltzen...)");

                    // Bizirik dauden Pokemon kopurua kalkulatu
                    int alive1 = _battle.AliveCountTeamA();
                    int alive2 = _battle.AliveCountTeamB();

                    // Irabazlea zerbitzarira bidali
                    _chatService?.SendBattleEnd(winner, _player1, _player2, alive1, alive2);
                }

                return;
            }
            else
            {           
                AppendLog($"Itxaroten txandako {_turnNumber + 1} mugimenduak...");
            }
        }

        // Talde baten Pokemonak zerbitzaritik kargatzen ditu
        private async Task<Pokemon[]> LoadTeamPokemonsAsync(string teamName)
        {
            return await Task.Run(() =>
            {
                string resp = ServerService.SendRequest("get_team", teamName ?? "", "");
                if (string.IsNullOrEmpty(resp)) return Array.Empty<Pokemon>();
                if (!resp.StartsWith("OK|")) return Array.Empty<Pokemon>();

                string json = resp.Substring(3);
                try
                {
                    var list = JsonSerializer.Deserialize<Pokemon[]>(json) ?? Array.Empty<Pokemon>();
                    return list.Take(6).ToArray();
                }
                catch
                {
                    return Array.Empty<Pokemon>();
                }
            });
        }

        // Jokalariarentzat talde bat aukeratzeko dialogoa
        private async Task<string?> SelectTeamForPlayerAsync(string playerName)
        {
            // Jokalariaren taldeak zerbitzaritik eskatu
            string resp = await Task.Run(() =>
                ServerService.SendRequest("get_teams", playerName, ""));

            if (string.IsNullOrEmpty(resp) || !resp.StartsWith("OK|"))
            {
                MessageBox.Show($"{playerName}-ren taldeak ezin izan dira lortu.");
                return null;
            }

            string json = resp.Substring(3);
            string[]? teams;
            try { teams = JsonSerializer.Deserialize<string[]>(json); }
            catch
            {
                MessageBox.Show($"{playerName}-ren talde formatua baliogabea.");
                return null;
            }

            teams ??= Array.Empty<string>();
            if (teams.Length == 0)
            {
                MessageBox.Show($"{playerName}-k ez ditu taldeak sortuak.");
                return null;
            }

            // Talde aukeraketa dialogoa
            var dlg = new TeamSelectWindow(playerName, teams);
            dlg.Owner = Application.Current.MainWindow;
            bool? ok = dlg.ShowDialog();
            return ok == true ? dlg.SelectedTeamName : null;
        }

        // Lobby-ra itzultzen du erabiltzailea
        private void VolverALobby()
        {
            var lobby = new LobbyView(ServerService.CurrentUsername ?? _player1);

            if (NavigationService != null) NavigationService.Navigate(lobby);
            else (Application.Current.MainWindow as MainWindow)?.MainFrame.Navigate(lobby);
        }

        // Amore emateko botoia
        private void SurrenderButton_Click(object sender, RoutedEventArgs e)
        {
            var currentUser = ServerService.CurrentUsername ?? "";
            var rivalUser = string.Equals(currentUser, _player1, StringComparison.OrdinalIgnoreCase) ? _player2 : _player1;

            // Bizirik dauden Pokemon kopurua kalkulatu
            int myAlive = _myTeam.Count(p => (p.HP ?? 0) > 0);
            int rivalAlive = _rivalTeam.Count(p => (p.HP ?? 0) > 0);

            // Amore emate mezua bidali eta lobby-ra itzuli
            _chatService?.SendSurrender(rivalUser, myAlive, rivalAlive);

            VolverALobby();
        }

        // Pokemonen HP maximoa hasieratzen du
        private static void InitializeMaxHp(Pokemon[] team)
        {
            const double HpMultiplier = 2.5;

            foreach (var p in team)
            {
                // MaxHP lehen aldiz ezartzen da
                if (p.MaxHP == null || p.MaxHP <= 0)
                    p.MaxHP = p.HP;

                // HP biderkatu egiten da lehen aldiz
                if (p.HP.HasValue && p.MaxHP.HasValue && p.HP.Value == p.MaxHP.Value)
                {
                    int scaled = (int)Math.Round(p.HP.Value * HpMultiplier, MidpointRounding.AwayFromZero);
                    p.HP = scaled;
                    p.MaxHP = scaled;
                }
            }
        }
    }
}