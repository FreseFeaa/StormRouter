using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using StormBase.Models;
using StormBase.Services;
using StormBase.Services.Routing;
using StormBase.Services.Storms;

class Program
{
    static void Main()
    {
        string jsonPath = "../StormBase.Tests/Data/route_data.json";

        if (!File.Exists(jsonPath))
        {
            Console.WriteLine($"Файл {jsonPath} не найден!");
            return;
        }

        var json = File.ReadAllText(jsonPath);
        var data = JsonSerializer.Deserialize<InputData>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (data == null)
        {
            Console.WriteLine("Ошибка чтения данных");
            return;
        }

        var routeGraph = new RouteGraph();
        var stormProvider = new StormProvider();

        var router = new StormRouter(routeGraph, stormProvider);

        router.LoadData(data);

        var results = router.CalculateOptimalRoutes(
            data.StartPoint,
            data.EndPoint,
            data.DepartureTime
        );

        Console.WriteLine("=== РЕЗУЛЬТАТЫ РАСЧЕТА МАРШРУТОВ ===");
        Console.WriteLine($"Отправление: {data.DepartureTime}");
        Console.WriteLine($"Из: {data.StartPoint} → В: {data.EndPoint}");
        Console.WriteLine();

        if (results.Count == 0)
        {
            Console.WriteLine("Маршрут не найден!");
            return;
        }

        for (int i = 0; i < results.Count; i++)
        {
            var route = results[i];
            Console.WriteLine($"═══════════════════════════════════════════");
            Console.WriteLine($"МАРШРУТ #{i + 1}");
            Console.WriteLine($"═══════════════════════════════════════════");
            Console.WriteLine($"Путь: {string.Join(" → ", route.Path)}");
            Console.WriteLine($"Время прибытия: {route.CurrentTime}");
            Console.WriteLine($"Общее время: {route.TotalTime:F1} ч, В пути: {route.TotalTravelTime:F1} ч, Ожидание: {route.TotalWaitTime:F1} ч");
            Console.WriteLine($"Общий риск: {route.TotalRisk:F1}");
            Console.WriteLine();
            Console.WriteLine("Сегменты:");

            for (int j = 0; j < route.Segments.Count; j++)
            {
                var seg = route.Segments[j];
                Console.WriteLine($"Шаг {j + 1}: {seg.Type} {seg.FromNode} → {seg.ToNode}");
                Console.WriteLine($"  Время: {seg.StartTime} → {seg.EndTime}");
                if (seg.Type == "Wait")
                {
                    Console.WriteLine($"  🕒 ОЖИДАНИЕ: {seg.Duration:F1} ч");
                }
                else
                {
                    Console.WriteLine($"  🚢 Движение: базовое {seg.BaseTime:F1} ч, фактическое {seg.ActualTime:F1} ч");
                    if (seg.StormSeverity != null)
                        Console.WriteLine($"     ⚠️  Шторм: {seg.StormSeverity}, замедление {seg.SlowdownCoefficient:F1}x, риск {seg.Risk:F1}");
                    else
                        Console.WriteLine($"     ✅ Без шторма");
                }
                Console.WriteLine();
            }

            var stormSegments = route.Segments.Where(s => s.StormSeverity != null).ToList();
            var waitSegments = route.Segments.Where(s => s.Type == "Wait").ToList();

            Console.WriteLine("Итоги маршрута:");
            Console.WriteLine($"  Всего шагов: {route.Segments.Count}");
            Console.WriteLine($"  Участков со штормом: {stormSegments.Count}");
            foreach (var seg in stormSegments)
                Console.WriteLine($"    - {seg.FromNode} → {seg.ToNode}: {seg.StormSeverity} (+{seg.Risk:F1}, {seg.SlowdownCoefficient:F1}x)");

            Console.WriteLine($"  Остановок для ожидания: {waitSegments.Count}");
            foreach (var seg in waitSegments)
                Console.WriteLine($"    - В узле {seg.FromNode}: {seg.Duration:F1} ч (с {seg.StartTime} по {seg.EndTime})");

            Console.WriteLine();
        }
    }
}
