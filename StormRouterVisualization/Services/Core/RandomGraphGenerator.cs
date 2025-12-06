using System;
using System.Collections.Generic;
using System.Linq;
using StormRouterVisualization.Models;

namespace StormRouterVisualization.Services
{
    public class RandomGraphGenerator
    {
        private readonly Random _random = new Random();

        public InputData? Generate(int minNodes = 10, int maxNodes = 30)
        {
            int nodeCount = _random.Next(minNodes, maxNodes + 1);
            var nodes = Enumerable.Range(1, nodeCount).Select(i => $"Node{i}").ToList();

            var routes = new List<Route>();
            var storms = new List<Storm>();

            // Стартовая и конечная точки
            string startPoint = nodes[_random.Next(nodes.Count)];
            string endPoint;
            do
            {
                endPoint = nodes[_random.Next(nodes.Count)];
            } while (endPoint == startPoint);

            // Связный граф через остовное дерево
            var connected = new HashSet<string> { startPoint };
            var unconnected = nodes.Where(n => n != startPoint).ToList();

            while (unconnected.Count > 0)
            {
                string from = connected.ElementAt(_random.Next(connected.Count));
                string to = unconnected[_random.Next(unconnected.Count)];

                double distance = _random.Next(50, 500);
                double baseTime = distance / 50.0;

                routes.Add(new Route { From = from, To = to, Distance = distance, BaseTime = baseTime });
                connected.Add(to);
                unconnected.Remove(to);
            }

            // Дополнительные ребра
            int additionalEdges = _random.Next(nodeCount, nodeCount * 2);
            var addedEdges = new HashSet<(string, string)>(routes.Select(r => (r.From, r.To)));

            for (int i = 0; i < additionalEdges; i++)
            {
                string from, to;
                int attempts = 0;
                do
                {
                    from = nodes[_random.Next(nodeCount)];
                    to = nodes[_random.Next(nodeCount)];
                    attempts++;
                    if (attempts > 50) break;
                } while (from == to || addedEdges.Contains((from, to)));

                if (attempts <= 50)
                {
                    double distance = _random.Next(50, 500);
                    double baseTime = distance / 50.0;

                    routes.Add(new Route { From = from, To = to, Distance = distance, BaseTime = baseTime });
                    addedEdges.Add((from, to));
                }
            }

            // Штормы
            foreach (var route in routes)
            {
                if (_random.NextDouble() < 0.15)
                {
                    DateTime stormStart = DateTime.Now.AddHours(_random.Next(-24, 48));
                    DateTime stormEnd = stormStart.AddHours(_random.Next(1, 12));
                    string severity = _random.Next(3) switch
                    {
                        0 => "low",
                        1 => "medium",
                        _ => "high"
                    };

                    storms.Add(new Storm
                    {
                        RouteId = $"{route.From}-{route.To}",
                        StartTime = stormStart,
                        EndTime = stormEnd,
                        Severity = severity
                    });
                }
            }

            var inputData = new InputData
            {
                StartPoint = startPoint,
                EndPoint = endPoint,
                DepartureTime = DateTime.Now,
                Routes = routes,
                Storms = storms
            };

            // Проверка, что маршрут существует
            var testRouter = new StormRouter();
            testRouter.LoadData(inputData);
            var results = testRouter.CalculateOptimalRoutes(startPoint, endPoint, DateTime.Now, 1);

            return results.Count > 0 ? inputData : null;
        }
    }
}
