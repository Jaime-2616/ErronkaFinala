using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Cliente.Services;

namespace Cliente.ViewModels
{
    public sealed class StatsReportViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string Title { get; private set; } = "Historia eta estatistikak";
        public string Subtitle { get; private set; } = "Partida orokorrak eta jokalariaren portaera ikusi.";
        public string ReportDate { get; private set; } = "";
        public string Source { get; private set; } = "BattleHistory (SQLite)";
        public string FiltersSummary { get; private set; } = "";

        public int TotalMatches { get; private set; }
        public int Wins { get; private set; }
        public int Losses { get; private set; }
        public int Surrenders { get; private set; }
        public double WinRate { get; private set; }
        public double AvgAliveOnWin { get; private set; }

        public ObservableCollection<ChartItem> ChartItems { get; } = new();
        public ObservableCollection<MatchRow> RecentMatches { get; } = new();

        public async Task LoadAsync(string username)
        {
            ReportDate = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
            FiltersSummary = $"Jokalaria: {username} | Data-tartea: guztiak | Aurkaria: Guztiak";
            OnPropertyChanged(nameof(ReportDate));
            OnPropertyChanged(nameof(FiltersSummary));

            string resp = await Task.Run(() => ServerService.SendRequest("get_battle_history", username, ""));
            if (string.IsNullOrEmpty(resp) || !resp.StartsWith("OK|"))
                return;

            string json = resp.Substring(3);
            var rows = JsonSerializer.Deserialize<BattleHistoryRow[]>(json) ?? Array.Empty<BattleHistoryRow>();

            TotalMatches = rows.Length;
            Wins = rows.Count(r => r.Result == "IRABAZI");
            Losses = rows.Count(r => r.Result == "GALDU");
            Surrenders = rows.Count(r => r.EndReason == "SURRENDER" && r.Result == "GALDU");
            WinRate = TotalMatches == 0 ? 0 : (Wins * 100.0 / TotalMatches);
            AvgAliveOnWin = rows.Where(r => r.Result == "IRABAZI")
                                .Select(r => r.MyAlive)
                                .DefaultIfEmpty(0)
                                .Average();

            ChartItems.Clear();
            foreach (var g in rows.GroupBy(r => r.Opponent))
                ChartItems.Add(new ChartItem(
                    g.Key,
                    g.Count(r => r.Result == "IRABAZI"),
                    g.Count(r => r.Result == "GALDU")));

            RecentMatches.Clear();
            foreach (var r in rows.Take(5))
                RecentMatches.Add(new MatchRow(
                    DateTime.TryParse(r.DateUtc, out var d) ? d.ToString("yyyy/MM/dd") : r.DateUtc,
                    r.Opponent,
                    r.Result,
                    r.EndReason,
                    $"{r.MyAlive} / {r.OpponentAlive}"));

            OnPropertyChanged(nameof(TotalMatches));
            OnPropertyChanged(nameof(Wins));
            OnPropertyChanged(nameof(Losses));
            OnPropertyChanged(nameof(Surrenders));
            OnPropertyChanged(nameof(WinRate));
            OnPropertyChanged(nameof(AvgAliveOnWin));
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private sealed class BattleHistoryRow
        {
            public string DateUtc { get; set; } = "";
            public string Opponent { get; set; } = "";
            public string Result { get; set; } = "";
            public string EndReason { get; set; } = "";
            public int MyAlive { get; set; }
            public int OpponentAlive { get; set; }
        }
    }
}