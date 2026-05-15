using System;
using System.Globalization;
using Cliente.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Cliente.Services
{
    public static class StatsReportPdfExporter
    {
        public static void Export(StatsReportViewModel vm, string filePath)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Column(col =>
                    {
                        col.Item().Text(vm.Title).FontSize(18).SemiBold();
                        col.Item().Text(vm.Subtitle).FontSize(11).FontColor(Colors.Grey.Darken2);
                        col.Item().PaddingTop(6).Row(row =>
                        {
                            row.RelativeItem().Text(vm.Source).FontColor(Colors.Grey.Darken2);
                            row.RelativeItem().AlignRight().Text(vm.ReportDate).FontColor(Colors.Grey.Darken2);
                        });
                        col.Item().PaddingTop(8).Text(vm.FiltersSummary).FontColor(Colors.Grey.Darken2);
                    });

                    page.Content().PaddingTop(15).Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                            {
                                c.Item().Text("Jokatutakoak").FontColor(Colors.Grey.Darken2);
                                c.Item().Text(vm.TotalMatches.ToString(CultureInfo.InvariantCulture)).FontSize(14).SemiBold();
                            });

                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                            {
                                c.Item().Text("Garaipen %").FontColor(Colors.Grey.Darken2);
                                c.Item().Text($"{vm.WinRate:F1}%").FontSize(14).SemiBold();
                            });

                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                            {
                                c.Item().Text("Surrender").FontColor(Colors.Grey.Darken2);
                                c.Item().Text(vm.Surrenders.ToString(CultureInfo.InvariantCulture)).FontSize(14).SemiBold();
                            });

                            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                            {
                                c.Item().Text("Avg Alive (Win)").FontColor(Colors.Grey.Darken2);
                                c.Item().Text($"{vm.AvgAliveOnWin:F1}").FontSize(14).SemiBold();
                            });
                        });

                        col.Item().PaddingTop(14).Text("Garaipenak aurkariaren arabera").SemiBold();
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.ConstantColumn(60);
                                columns.ConstantColumn(60);
                                columns.ConstantColumn(60);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Aurkaria").SemiBold();
                                header.Cell().AlignRight().Text("Wins").SemiBold();
                                header.Cell().AlignRight().Text("Losses").SemiBold();
                                header.Cell().AlignRight().Text("Win %").SemiBold();
                            });

                            foreach (var item in vm.ChartItems)
                            {
                                table.Cell().Text(item.Opponent);
                                table.Cell().AlignRight().Text(item.Wins.ToString(CultureInfo.InvariantCulture));
                                table.Cell().AlignRight().Text(item.Losses.ToString(CultureInfo.InvariantCulture));
                                table.Cell().AlignRight().Text($"{item.WinRate:F1}%");
                            }
                        });

                        col.Item().PaddingTop(14).Text("Azken 5 partidak").SemiBold();
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Data").SemiBold();
                                header.Cell().Text("Aurkaria").SemiBold();
                                header.Cell().Text("Emaitza").SemiBold();
                                header.Cell().Text("Arrazoia").SemiBold();
                                header.Cell().Text("Bizirik").SemiBold();
                            });

                            foreach (var r in vm.RecentMatches)
                            {
                                table.Cell().Text(r.Date);
                                table.Cell().Text(r.Opponent);
                                table.Cell().Text(r.Result);
                                table.Cell().Text(r.EndReason);
                                table.Cell().Text(r.AliveSummary);
                            }
                        });
                    });

                    page.Footer().AlignRight().Text(text =>
                    {
                        text.Span("Sortua: ");
                        text.Span(DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
                    });
                });
            }).GeneratePdf(filePath);
        }
    }
}