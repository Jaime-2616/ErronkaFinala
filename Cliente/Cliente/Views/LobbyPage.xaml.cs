using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Cliente.Services;
using Cliente.Views;

namespace Cliente.Views
{
    public partial class LobbyView : UserControl
    {
        // Uneko erabiltzailearen izena
        private readonly string _username;
        private CancellationTokenSource? _cts;
        private ChatService? _chatService;
        private ObservableCollection<string> _messages = new();
        private string[] _top5 = Array.Empty<string>();

        public LobbyView(string username)
        {
            InitializeComponent();
            _username = username;

            // Jokalarien eta txataren zerrendak hasieratu
            PlayersListBox.ItemsSource = Array.Empty<string>();
            ChatListBox.ItemsSource = _messages;

            try
            {
                // Txat zerbitzariarekin konektatu eta ekitaldiak harpidetu
                _chatService = new ChatService(_username);
                _chatService.MessageReceived += OnChatMessageReceived;
                _chatService.ChallengeReceived += OnChallengeReceived;
                _chatService.BattleStarted += OnBattleStarted;
                _chatService.ChallengeRejected += OnChallengeRejected;
                _chatService.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ezin izan da txatera konektatu: " + ex.Message);
            }

            // Jokalarien zerrenda eguneratzeko polling-a hasi
            _cts = new CancellationTokenSource();
            _ = StartPlayersPollingAsync(_cts.Token);
        }

        // Txat mezua jasotzean zerrendara gehitzen du
        private void OnChatMessageReceived(string sender, string text)
        {
            Dispatcher.Invoke(() =>
            {
                _messages.Add($"{sender}: {text}");
                ChatListBox.ScrollIntoView(_messages.Last());
            });
        }

        // Erronka jasotzean, onartu edo baztertu galdetzen du
        private void OnChallengeReceived(string fromUser)
        {
            Dispatcher.Invoke(() =>
            {
                var result = MessageBox.Show(
                    $"{fromUser} jokalariak borroka batera erronka bota dizu.\n\nOnartu?",
                    "Erronka jasoa",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                bool accept = (result == MessageBoxResult.Yes);
                _chatService?.SendChallengeResponse(fromUser, accept);
            });
        }

        // Erronka baztertua jasotzean, mezua erakusten du
        private void OnChallengeRejected(string byUser)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    $"{byUser} jokalariak zure erronka baztertu du.",
                    "Erronka baztertua",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            });
        }

        // Borroka hasten denean, borroka orrira nabigatzen du
        private void OnBattleStarted(string player1, string player2)
        {
            Console.WriteLine($"[LobbyView {_username}] OnBattleStarted: {player1} vs {player2}");

            Dispatcher.Invoke(() =>
            {
                Console.WriteLine($"[LobbyView {_username}] CombatPage-ra nabigatzen...");
                OpenCombatPage(player1, player2);
            });
        }

        // Borroka orria irekitzen du
        private void OpenCombatPage(string player1, string player2)
        {
            Console.WriteLine($"[LobbyView {_username}] OpenCombatPage deituta: {player1}, {player2}");

            var combat = new CombatPage(player1, player2);

            var parentPage = this.FindParentPage();
            if (parentPage?.NavigationService != null)
            {
                Console.WriteLine("[LobbyView] parentPage.NavigationService erabiltzen");
                parentPage.NavigationService.Navigate(combat);
                return;
            }

            var main = Application.Current.MainWindow as MainWindow;
            if (main?.MainFrame != null)
            {
                Console.WriteLine("[LobbyView] MainWindow.MainFrame erabiltzen");
                main.MainFrame.Navigate(combat);
            }
            else
            {
                Console.WriteLine("[LobbyView] Ez da NavigationService edo MainFrame aurkitu");
            }
        }

        // Jokalarien zerrenda eta puntuak aldian-aldian eguneratzen ditu
        private async Task StartPlayersPollingAsync(CancellationToken token)
        {
            await LoadPlayersOnceAsync();

            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), token);
                    if (token.IsCancellationRequested) break;
                    await LoadPlayersOnceAsync();
                }
            }
            catch (TaskCanceledException) { }
        }

        // Jokalarien zerrenda, puntu propioak eta top 5 sailkapena kargatzen ditu
        private Task LoadPlayersOnceAsync()
        {
            return Task.Run(() =>
            {
                // Jokalarien zerrenda puntuekin
                string response = ServerService.SendRequest("get_players_with_points", _username, "");
                if (!string.IsNullOrEmpty(response) && response.StartsWith("OK|"))
                {
                    string csv = response.Substring(3);

                    var items = string.IsNullOrWhiteSpace(csv)
                        ? Array.Empty<string>()
                        : csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                             .Select(s => s.Trim())
                             .Where(s => !string.IsNullOrEmpty(s))
                             .Select(s =>
                             {
                                 var parts = s.Split(':');
                                 if (parts.Length == 2 && int.TryParse(parts[1], out int pts))
                                     return $"{parts[0]} ({pts})";
                                 return s;
                             })
                             .ToArray();

                    Dispatcher.Invoke(() => PlayersListBox.ItemsSource = items);
                }
                else
                {
                    Dispatcher.Invoke(() => PlayersListBox.ItemsSource = Array.Empty<string>());
                }

                // Nire puntuak
                string myResp = ServerService.SendRequest("get_points", _username, "");
                if (!string.IsNullOrEmpty(myResp) && myResp.StartsWith("OK|"))
                {
                    var v = myResp.Substring(3).Trim();
                    Dispatcher.Invoke(() => MyPointsText.Text = $"Puntuak: {v}");
                }

                // Top 5 sailkapena
                string topResp = ServerService.SendRequest("get_top5", "", "");
                if (!string.IsNullOrEmpty(topResp) && topResp.StartsWith("OK|"))
                {
                    string csv = topResp.Substring(3);

                    var rows = string.IsNullOrWhiteSpace(csv)
                        ? Array.Empty<LeaderboardRow>()
                        : csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                             .Select(s => s.Trim())
                             .Where(s => !string.IsNullOrEmpty(s))
                             .Select((s, i) =>
                             {
                                 // espero den formatua: "erabiltzailea:puntuak"
                                 var p = s.Split(':');
                                 string user = p.Length >= 1 ? p[0].Trim() : "";
                                 int pts = (p.Length >= 2 && int.TryParse(p[1], out var parsed)) ? parsed : 0;

                                 return new LeaderboardRow
                                 {
                                     Rank = i + 1,
                                     Username = user,
                                     Points = pts
                                 };
                             })
                             .ToArray();

                    Dispatcher.Invoke(() => Top5ListBox.ItemsSource = rows);
                }
                else
                {
                    Dispatcher.Invoke(() => Top5ListBox.ItemsSource = Array.Empty<LeaderboardRow>());
                }
            });
        }

        // Saioa ixteko botoia
        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            try
            {
                if (_chatService != null)
                {
                    _chatService.MessageReceived -= OnChatMessageReceived;
                    _chatService.ChallengeReceived -= OnChallengeReceived;
                    _chatService.BattleStarted -= OnBattleStarted;
                    _chatService.ChallengeRejected -= OnChallengeRejected;
                }
                _chatService?.Disconnect();
                _chatService = null;
            }
            catch { }

            try { ServerService.SendRequest("logout", _username, ""); } catch { }

            // Saioa ixtean erabiltzailea garbitu
            ServerService.CurrentUsername = null;

            // Login orrira itzuli
            var loginPage = new LoginPage(username =>
            {
                ServerService.CurrentUsername = username;
            });

            var parentPage = this.FindParentPage();
            if (parentPage?.NavigationService != null)
            {
                parentPage.NavigationService.Navigate(loginPage);
                return;
            }
            var main = Application.Current.MainWindow as MainWindow;
            main?.MainFrame.Navigate(loginPage);
        }

        // Txat mezua bidaltzeko botoia
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendChatFromInput();
        }

        // Enter sakatzean txat mezua bidaltzen du
        private void ChatTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SendChatFromInput();
                e.Handled = true;
            }
        }

        // Txat mezua bidaltzen du eta testua garbitzen du
        private void SendChatFromInput()
        {
            var text = ChatTextBox.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            try
            {
                _chatService?.SendMessage(text);
            }
            catch
            {
                MessageBox.Show("Errorea mezua bidaltzean.");
            }
            finally
            {
                ChatTextBox.Clear();
            }
        }

        // Pokedex eta taldeak orrira nabigatzen du
        private void PokedexButton_Click(object sender, RoutedEventArgs e)
        {
            var teams = new TeamsPage();
            var parentPage = this.FindParentPage();
            if (parentPage?.NavigationService != null)
            {
                parentPage.NavigationService.Navigate(teams);
                return;
            }
            var main = Application.Current.MainWindow as MainWindow;
            main?.MainFrame.Navigate(teams);
        }

        // Erronka bidaltzeko botoia
        private void ChallengeButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlayersListBox.SelectedItem is not string selected || string.IsNullOrWhiteSpace(selected))
            {
                MessageBox.Show(
                    "Lehenengo erronkatu nahi duzun jokalari bat hautatu.",
                    "Hautaketarik ez",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            string targetUser = selected.Split('(')[0].Trim();

            if (string.IsNullOrWhiteSpace(targetUser))
                return;

            if (string.Equals(targetUser, _username, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "Ezin duzu zeure burua erronkatu.",
                    "Erronka baliogabea",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                _chatService?.SendChallenge(targetUser);
                MessageBox.Show(
                    $"{targetUser} jokalariari erronka bidali diozu.",
                    "Erronka bidalia",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Ezin izan da erronka bidali: " + ex.Message,
                    "Errorea",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // Estatistikak eta historia ikusteko botoia
        private void HistoryButton_Click(object sender, RoutedEventArgs e)
        {
            var wnd = new StatsWindow(_username)
            {
                Owner = Application.Current.MainWindow
            };
            wnd.ShowDialog();
        }

        // Top 5 sailkapenaren datuak
        private sealed class LeaderboardRow
        {
            public int Rank { get; init; }
            public string Username { get; init; } = "";
            public int Points { get; init; }
        }
    }

    // VisualTree-n guraso Page elementua bilatzeko laguntzailea
    static class VisualTreeHelpers
    {
        public static Page FindParentPage(this DependencyObject child)
        {
            DependencyObject current = child;
            while (current != null)
            {
                if (current is Page p) return p;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}