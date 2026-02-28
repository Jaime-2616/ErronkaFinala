using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;
using System.IO;
using System.Printing;
using System.IO.Packaging;
using Cliente.Services;
using System.Windows.Controls;

namespace Cliente.Views
{
    public partial class StatsWindow : Window
    {
        // Uneko erabiltzailearen izena
        private readonly string _username;

        public StatsWindow(string username) 
        {
            InitializeComponent();
            _username = username;

            // Estatistika globalak eta jokalariarenak kargatzen ditu
            LoadGlobalStats();
            LoadPlayerStats();
        }

        // Estatistika globalak kargatzen ditu (top 10 eta gehien erabilitako Pokemon-a)
        private async void LoadGlobalStats()
        {
            try
            {
                // Top 10 jokalariak zerbitzaritik lortzen ditu
                string respTop = await System.Threading.Tasks.Task.Run(
                    () => ServerService.SendRequest("get_top5", "", ""));

                if (!string.IsNullOrEmpty(respTop) && respTop.StartsWith("OK|"))
                {
                    string csv = respTop.Substring(3);
                    var rows = string.IsNullOrWhiteSpace(csv)
                        ? Array.Empty<string>()
                        : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    var display = rows
                        .Select((s, i) => $"{i + 1}. {s.Replace(":", " - ")}")
                        .ToArray();

                    Top10ListBox.ItemsSource = display;
                }

                // Gehien erabilitako Pokemon-a global mailan lortzen du
                string respPk = await System.Threading.Tasks.Task.Run(
                    () => ServerService.SendRequest("get_most_picked_pokemon_global", "", ""));

                if (!string.IsNullOrEmpty(respPk) && respPk.StartsWith("OK|"))
                {
                    string data = respPk.Substring(3);
                    var parts = data.Split(':');
                    string name = parts.Length >= 1 ? parts[0] : "";
                    string cntStr = parts.Length >= 2 ? parts[1] : "0";

                    if (string.IsNullOrWhiteSpace(name))
                        GlobalMostUsedPokemonText.Text = "Ez dago taldeen daturik oraindik.";
                    else
                        GlobalMostUsedPokemonText.Text = $"{name} (taldeetan {cntStr} aldiz agertzen da)";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Errorea estatistika globalak kargatzean: " + ex.Message);
            }
        }

        // Jokalariaren estatistikak kargatzen ditu
        private async void LoadPlayerStats()
        {
            try
            {
                string resp = await Task.Run(
                    () => ServerService.SendRequest("get_player_stats", _username, ""));

                if (string.IsNullOrEmpty(resp) || !resp.StartsWith("OK|"))
                {
                    PlayerMostUsedPokemonText.Text = "Ez dago jokalariaren daturik.";
                    return;
                }

                string json = resp.Substring(3);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Estatistika nagusiak lortzen ditu: gehien erabilitako Pokemon-a, garaipenak, porrotak, uzteak, bataz besteko bizirik
                string mostUsed = root.TryGetProperty("MostUsedPokemon", out var mu) ? mu.GetString() ?? "" : "";
                int totalBattles = root.TryGetProperty("TotalBattles", out var tb) ? tb.GetInt32() : 0;
                double winPct = root.TryGetProperty("WinPct", out var wp) ? wp.GetDouble() : 0;
                double lossPct = root.TryGetProperty("LossPct", out var lp) ? lp.GetDouble() : 0;
                double surrenderPct = root.TryGetProperty("SurrenderPct", out var sp) ? sp.GetDouble() : 0;
                double avgAlive = root.TryGetProperty("AvgAlive", out var aa) ? aa.GetDouble() : 0;

                if (string.IsNullOrWhiteSpace(mostUsed))
                    PlayerMostUsedPokemonText.Text = "Taldeetan gehien erabilitako Pokémon-a: (daturik ez)";
                else
                    PlayerMostUsedPokemonText.Text = $"Taldeetan gehien erabilitako Pokémon-a: {mostUsed}";

                PlayerWinPctText.Text = $"Garaipenak: {winPct:F1}%";
                PlayerLossPctText.Text = $"Porrotak: {lossPct:F1}%";
                PlayerSurrenderPctText.Text = $"Uzteak: {surrenderPct:F1}%";
                PlayerAvgAliveText.Text = $"Bataz besteko Pokémon bizirik bukaeran: {avgAlive:F2}";

                if (totalBattles == 0)
                {
                    PlayerWinPctText.Text += " (borrokarik gabe)";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Errorea jokalariaren estatistikak kargatzean: " + ex.Message);
            }
        }

        // Leihoa ixteko botoia
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // PDF sortzeko botoia: estatistikak dokumentu batean inprimatzen ditu
        private void BtnPrintPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var doc = new FlowDocument
                {
                    PagePadding = new Thickness(40),
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                    FontSize = 12
                };

                doc.Blocks.Add(new Paragraph(new Run("Historia eta estatistikak"))
                {
                    FontSize = 20,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 10)
                });

                // Atal globala eta jokalariaren datuak gehitzen ditu dokumentura
                doc.Blocks.Add(new Paragraph(new Run("Atal globala"))
                {
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 10, 0, 4)
                });

                if (Top10ListBox.ItemsSource is IEnumerable<string> topItems)
                {
                    doc.Blocks.Add(new Paragraph(new Run("Top 10 jokalari:")));
                    var list = new List();
                    foreach (var item in topItems)
                        list.ListItems.Add(new ListItem(new Paragraph(new Run(item))));
                    doc.Blocks.Add(list);
                }

                doc.Blocks.Add(new Paragraph(new Run(
                    "Munduan gehien erabilitako Pokemona: " +
                    (GlobalMostUsedPokemonText.Text ?? string.Empty)))
                {
                    Margin = new Thickness(0, 6, 0, 0)
                });

                doc.Blocks.Add(new Paragraph(new Run("Jokalariaren datuak"))
                {
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 12, 0, 4)
                });

                void AddLine(string text)
                {
                    if (!string.IsNullOrWhiteSpace(text))
                        doc.Blocks.Add(new Paragraph(new Run(text)));
                }

                AddLine(PlayerMostUsedPokemonText.Text);
                AddLine(PlayerWinPctText.Text);
                AddLine(PlayerLossPctText.Text);
                AddLine(PlayerSurrenderPctText.Text);
                AddLine(PlayerAvgAliveText.Text);

                // PDF sortzeko sistemako inprimagailua erabiltzen du
                var printDlg = new PrintDialog();
                if (printDlg.ShowDialog() != true)
                    return;

                if (printDlg.PrintQueue == null)
                {
                    MessageBox.Show("Ez da inprimagailurik aurkitu.",
                                    "Errorea",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                    return;
                }

                doc.PageHeight = printDlg.PrintableAreaHeight;
                doc.PageWidth = printDlg.PrintableAreaWidth;
                doc.ColumnWidth = printDlg.PrintableAreaWidth;

                var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                var writer = PrintQueue.CreateXpsDocumentWriter(printDlg.PrintQueue);
                var printTicket = printDlg.PrintTicket ?? new PrintTicket();

                writer.Write(paginator, printTicket);

                MessageBox.Show("PDF-a sortu da. Egiaztatu hautatutako inprimagailuaren irteera-karpeta.",
                                "Informazioa",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Errorea PDF-a prestatzean: " + ex.Message,
                                "Errorea",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }
    }
}