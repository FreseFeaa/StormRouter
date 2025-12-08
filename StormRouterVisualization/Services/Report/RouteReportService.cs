using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;           
using System.Windows.Controls; 
using System.Windows.Media;    
using StormBase.Models;

namespace StormRouterVisualization.Services
{
    public class RouteReportService
    {
        private readonly Dictionary<string, Style> _styles;

        public RouteReportService(Dictionary<string, Style> styles)
        {
            _styles = styles;
        }

        public string GenerateRouteDetails(InputData? inputData, List<RouteState>? routeStates)
        {
            if (routeStates == null || routeStates.Count == 0)
                return "❌ Маршруты не найдены";

            var sb = new StringBuilder();

            for (int i = 0; i < routeStates.Count; i++)
            {
                var result = routeStates[i];
                sb.AppendLine($"═══════════════════════════════════════════════════");
                sb.AppendLine($"🚢 МАРШРУТ #{i + 1}");
                sb.AppendLine($"═══════════════════════════════════════════════════");
                sb.AppendLine($"📊 Общая информация:");
                sb.AppendLine($"  • Путь: {string.Join(" → ", result.Path)}");
                sb.AppendLine($"  • Время отправления: {inputData?.DepartureTime:dd.MM.yyyy HH:mm}");
                sb.AppendLine($"  • Время прибытия: {result.CurrentTime:dd.MM.yyyy HH:mm}");
                sb.AppendLine($"  • Общее время в пути: {result.TotalTime:F1} часов");
                sb.AppendLine($"  • Чистое время движения: {result.TotalTravelTime:F1} часов");
                sb.AppendLine($"  • Время ожидания: {result.TotalWaitTime:F1} часов");
                sb.AppendLine($"  • Общий риск: {result.TotalRisk:F1}");
                sb.AppendLine();

                sb.AppendLine($"🔄 Детализация маршрута:");
                sb.AppendLine($"---------------------------------------------------");

                for (int j = 0; j < result.Segments.Count; j++)
                {
                    var segment = result.Segments[j];
                    if (segment.Type == "Wait")
                    {
                        sb.AppendLine($"⏳ Шаг {j + 1}: ОЖИДАНИЕ в узле {segment.FromNode}");
                        sb.AppendLine($"     📅 Время: {segment.StartTime:dd.MM HH:mm} → {segment.EndTime:dd.MM HH:mm}");
                        sb.AppendLine($"     ⏱️  Продолжительность: {segment.Duration:F1} часов");
                        sb.AppendLine($"     📋 Причина: ожидание окончания шторма");
                    }
                    else if (segment.Type == "Travel")
                    {
                        sb.AppendLine($"🚢 Шаг {j + 1}: ДВИЖЕНИЕ {segment.FromNode} → {segment.ToNode}");
                        sb.AppendLine($"     📅 Время: {segment.StartTime:dd.MM HH:mm} → {segment.EndTime:dd.MM HH:mm}");
                        sb.AppendLine($"     ⏱️  Базовая продолжительность: {segment.BaseTime:F1} часов");
                        sb.AppendLine($"     ⏱️  Фактическая продолжительность: {segment.ActualTime:F1} часов");

                        if (!string.IsNullOrEmpty(segment.StormSeverity))
                        {
                            var coefficients = GraphVisualizer.GetStormCoefficients(segment.StormSeverity);
                            sb.AppendLine($"     ⚡ ШТОРМ: уровень {segment.StormSeverity}");
                            sb.AppendLine($"     📈 Коэффициент замедления: {coefficients.slowdown:F1}x");
                            sb.AppendLine($"     🎯 Добавочный риск: {coefficients.risk}");
                        }
                        else
                        {
                            sb.AppendLine($"     ✅ Без шторма");
                            sb.AppendLine($"     🎯 Добавочный риск: 0");
                        }
                    }
                    sb.AppendLine();
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public void PopulateStatisticsPanel(StackPanel panel, InputData? inputData, List<RouteState>? routeStates, Dictionary<string, Point> nodePositions, TimeSpan computationTime)
        {
            panel.Children.Clear();
            if (routeStates == null || routeStates.Count == 0) return;

            var bestRoute = routeStates[0];

            AddStatistic(panel, "Лучший маршрут", "");

            var pathTextBox = new TextBox
            {
                Text = string.Join(" → ", bestRoute.Path),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Background = Brushes.Transparent,
                BorderThickness = new System.Windows.Thickness(0),
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 80,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new System.Windows.Thickness(0, 0, 0, 10)
            };
            panel.Children.Add(pathTextBox);

            AddStatistic(panel, "Время отправления", $"{inputData?.DepartureTime:dd.MM.yyyy HH:mm}");
            AddStatistic(panel, "Время прибытия", $"{bestRoute.CurrentTime:dd.MM.yyyy HH:mm}");
            AddStatistic(panel, "Общее время в пути", $"{bestRoute.TotalTime:F1} часов");
            AddStatistic(panel, "Время движения", $"{bestRoute.TotalTravelTime:F1} часов");
            AddStatistic(panel, "Время ожидания", $"{bestRoute.TotalWaitTime:F1} часов");
            AddStatistic(panel, "Общий риск", $"{bestRoute.TotalRisk:F1}");
            AddStatistic(panel, "Время вычисления", $"{computationTime.TotalMilliseconds:F2} мс");

            var stormSegments = bestRoute.Segments.Where(s => !string.IsNullOrEmpty(s.StormSeverity)).ToList();
            AddStatistic(panel, "Участков со штормом", stormSegments.Count.ToString());
            foreach (var stormSegment in stormSegments)
            {
                var coefficients = GraphVisualizer.GetStormCoefficients(stormSegment.StormSeverity);
                AddStatistic(panel, $"  - {stormSegment.FromNode}→{stormSegment.ToNode}", $"{stormSegment.StormSeverity} (риск +{coefficients.risk})");
            }

            var waitSegments = bestRoute.Segments.Where(s => s.Type == "Wait").ToList();
            AddStatistic(panel, "Остановок для ожидания", waitSegments.Count.ToString());
            foreach (var waitSegment in waitSegments)
            {
                AddStatistic(panel, $"  - В узле {waitSegment.FromNode}", $"{waitSegment.Duration:F1} часов");
            }

            AddStatistic(panel, "Всего узлов в графе", nodePositions.Count.ToString());
            AddStatistic(panel, "Всего рёбер в графе", inputData?.Routes?.Count.ToString() ?? "0");
            AddStatistic(panel, "Найдено маршрутов", routeStates.Count.ToString());
        }

        private void AddStatistic(StackPanel panel, string name, string value)
        {
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new System.Windows.Thickness(0, 4, 0, 4)
            };

            var nameText = new TextBlock
            {
                Text = name + ":",
                FontWeight = FontWeights.Bold,
                Width = 180,
                Style = _styles.ContainsKey("StatTextBlock") ? _styles["StatTextBlock"] : null
            };

            var valueText = new TextBlock
            {
                Text = value,
                Style = _styles.ContainsKey("StatTextBlock") ? _styles["StatTextBlock"] : null
            };

            stackPanel.Children.Add(nameText);
            stackPanel.Children.Add(valueText);
            panel.Children.Add(stackPanel);
        }

        public string FormatRawData(string jsonString)
        {
            try
            {
                var formattedJson = JsonSerializer.Serialize(
                    JsonSerializer.Deserialize<JsonElement>(jsonString),
                    new JsonSerializerOptions { WriteIndented = true }
                );
                return formattedJson;
            }
            catch
            {
                return jsonString;
            }
        }
    }
}
