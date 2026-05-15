using System;
using System.Collections.ObjectModel;

namespace Cliente.ViewModels
{
    public sealed class ReportPrototypeViewModel
    {
        public string Title { get; } = "JOKALARIAREN ESTATISTIKAK ETA HISTORIA";
        public string Subtitle { get; } = "Partida orokorrak eta jokalariaren portaera ikusi.";
        public string ReportDate { get; } = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
        public string Source { get; } = "BattleHistory (SQLite)";
        public string FiltersSummary { get; } =
            "Jokalaria: Ash | Data-tartea: 2024/05/01 - 2024/05/10 | Aurkaria: Guztiak";

        public int TotalMatches { get; } = 20;
        public int Wins { get; } = 12;
        public int Losses { get; } = 8;
        public int Surrenders { get; } = 1;
        public double WinRate { get; } = 60;
        public double AvgAliveOnWin { get; } = 2.8;

        public ObservableCollection<ChartItem> ChartItems { get; } = new()
        {
            new ChartItem("Gary", 5, 2),
            new ChartItem("Brock", 3, 1),
            new ChartItem("Misty", 2, 3),
            new ChartItem("Lance", 2, 0),
            new ChartItem("Giovanni", 1, 2)
        };

        public ObservableCollection<MatchRow> RecentMatches { get; } = new()
        {
            new MatchRow("2024/05/10", "Gary", "IRABAZI", "NORMAL", "2 / 0"),
            new MatchRow("2024/05/10", "Misty", "IRABAZI", "SURRENDER", "5 / 1"),
            new MatchRow("2024/05/09", "Brock", "IRABAZI", "NORMAL", "3 / 0"),
            new MatchRow("2024/05/09", "Giovanni", "GALDU", "NORMAL", "0 / 3"),
            new MatchRow("2024/05/08", "Giovanni", "IRABAZI", "NORMAL", "1 / 0")
        };
    }

    public sealed class ChartItem
    {
        public string Opponent { get; }
        public int Wins { get; }
        public int Losses { get; }
        public int Total => Wins + Losses;
        public double WinRate => Total == 0 ? 0 : (Wins * 100.0 / Total);

        public ChartItem(string opponent, int wins, int losses)
        {
            Opponent = opponent;
            Wins = wins;
            Losses = losses;
        }
    }

    public sealed class MatchRow
    {
        public string Date { get; }
        public string Opponent { get; }
        public string Result { get; }
        public string EndReason { get; }
        public string AliveSummary { get; }

        public MatchRow(string date, string opponent, string result, string endReason, string aliveSummary)
        {
            Date = date;
            Opponent = opponent;
            Result = result;
            EndReason = endReason;
            AliveSummary = aliveSummary;
        }
    }
}