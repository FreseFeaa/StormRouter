using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StormRoutingAlgorithm
{
    // Классы для десериализации JSON
    public class InputData
    {
        public string StartPoint { get; set; } = "";
        public string EndPoint { get; set; } = "";
        public DateTime DepartureTime { get; set; }
        public List<Route> Routes { get; set; } = new List<Route>();
        public List<Storm> Storms { get; set; } = new List<Storm>();
    }

    public class Route
    {
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public double Distance { get; set; }
        public double BaseTime { get; set; }
    }

    public class Storm
    {
        public string RouteId { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Severity { get; set; } = "";
    }

    // Детальная информация о каждом сегменте пути
    public class RouteSegment
    {
        public string Type { get; set; } = ""; // "Travel" или "Wait"
        public string FromNode { get; set; } = "";
        public string ToNode { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double Duration { get; set; }
        public double Risk { get; set; }
        public string? StormSeverity { get; set; }
        public double BaseTime { get; set; }
        public double ActualTime { get; set; }
        public double SlowdownCoefficient { get; set; } = 1.0;
    }

    // Класс для хранения состояния алгоритма
    public class RouteState
    {
        public string Node { get; set; } = "";
        public DateTime CurrentTime { get; set; }
        public double TotalRisk { get; set; }
        public List<string> Path { get; set; } = new List<string>();
        public double TotalTime { get; set; }
        public double TotalTravelTime { get; set; }
        public double TotalWaitTime { get; set; }
        public List<RouteSegment> Segments { get; set; } = new List<RouteSegment>();
    }

    // Основной класс алгоритма
    public class StormRouter
    {
        private Dictionary<string, List<Route>> _graph = new Dictionary<string, List<Route>>();
        private Dictionary<string, List<Storm>> _stormsByRoute = new Dictionary<string, List<Storm>>();
        private Dictionary<string, (double slowdown, int risk)> _stormCoefficients = new Dictionary<string, (double, int)>();

        public StormRouter()
        {
            InitializeStormCoefficients();
        }

        private void InitializeStormCoefficients()
        {
            _stormCoefficients = new Dictionary<string, (double, int)>
            {
                ["low"] = (1.2, 1),
                ["medium"] = (1.5, 2),
                ["high"] = (2.0, 3)
            };
        }

        public void LoadData(InputData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            
            BuildGraph(data.Routes);
            BuildStormIndex(data.Storms);
        }

        private void BuildGraph(List<Route> routes)
        {
            _graph = new Dictionary<string, List<Route>>();
            foreach (var route in routes)
            {
                if (!_graph.ContainsKey(route.From))
                    _graph[route.From] = new List<Route>();
                _graph[route.From].Add(route);
            }
        }

        private void BuildStormIndex(List<Storm> storms)
        {
            _stormsByRoute = new Dictionary<string, List<Storm>>();
            foreach (var storm in storms)
            {
                if (!_stormsByRoute.ContainsKey(storm.RouteId))
                    _stormsByRoute[storm.RouteId] = new List<Storm>();
                _stormsByRoute[storm.RouteId].Add(storm);
            }
        }

        // Основной метод расчета маршрута
        public List<RouteState> CalculateOptimalRoutes(string startPoint, string endPoint, DateTime departureTime, int maxAlternatives = 3)
        {
            var results = new List<RouteState>();
            var visited = new Dictionary<string, List<RouteState>>();
            var queue = new PriorityQueue<RouteState, double>();

            // Инициализация начального состояния
            var initialState = new RouteState
            {
                Node = startPoint,
                CurrentTime = departureTime,
                TotalRisk = 0,
                Path = new List<string> { startPoint },
                TotalTime = 0,
                TotalTravelTime = 0,
                TotalWaitTime = 0,
                Segments = new List<RouteSegment>()
            };

            queue.Enqueue(initialState, 0);

            while (queue.Count > 0 && results.Count < maxAlternatives)
            {
                var currentState = queue.Dequeue();

                // Если достигли конечной точки
                if (currentState.Node == endPoint)
                {
                    results.Add(currentState);
                    continue;
                }

                // Получаем все возможные следующие шаги
                var nextSteps = GetNextSteps(currentState);
                
                foreach (var nextStep in nextSteps)
                {
                    if (nextStep == null) continue;
                    
                    // Проверяем, не посещали ли мы уже этот узел в худшем состоянии
                    if (!IsStateWorseThanExisting(visited, nextStep))
                    {
                        AddToVisited(visited, nextStep);
                        
                        // Эвристика: предполагаемое оставшееся время + общее время
                        double priority = nextStep.TotalTime; 
                        queue.Enqueue(nextStep, priority);
                    }
                }
            }

            return results;
        }

        private List<RouteState?> GetNextSteps(RouteState currentState)
        {
            var nextSteps = new List<RouteState?>();

            if (!_graph.ContainsKey(currentState.Node))
                return nextSteps;

            foreach (var route in _graph[currentState.Node])
            {
                // Вариант 1: Ехать сразу
                var immediateState = CalculateRouteStep(currentState, route, wait: false);
                if (immediateState != null)
                    nextSteps.Add(immediateState);

                // Вариант 2: Ждать окончания шторма (если он есть)
                var waitState = CalculateRouteStep(currentState, route, wait: true);
                if (waitState != null)
                    nextSteps.Add(waitState);
            }

            return nextSteps;
        }

        private RouteState? CalculateRouteStep(RouteState currentState, Route route, bool wait)
        {
            string routeId = $"{route.From}-{route.To}";
            DateTime startTime = currentState.CurrentTime;
            double travelTime = route.BaseTime;
            double riskAddition = 0;
            double slowdownCoefficient = 1.0;
            string? stormSeverity = null;

            // Проверяем активные штормы на маршруте
            var activeStorm = GetActiveStorm(routeId, startTime);
            
            // Создаем новый сегмент для ожидания (если нужно)
            RouteSegment? waitSegment = null;
            
            if (activeStorm != null)
            {
                if (wait)
                {
                    // Ждем окончания шторма
                    DateTime waitUntil = activeStorm.EndTime;
                    if (waitUntil <= startTime)
                        return null; // Ожидание бессмысленно

                    double waitHours = (waitUntil - startTime).TotalHours;
                    
                    // Создаем сегмент ожидания
                    waitSegment = new RouteSegment
                    {
                        Type = "Wait",
                        FromNode = currentState.Node,
                        ToNode = currentState.Node,
                        StartTime = startTime,
                        EndTime = waitUntil,
                        Duration = waitHours,
                        Risk = 0,
                        BaseTime = waitHours,
                        ActualTime = waitHours
                    };
                    
                    startTime = waitUntil;
                    
                    // Проверяем, остался ли шторм после ожидания
                    var stormAfterWait = GetActiveStorm(routeId, waitUntil);
                    if (stormAfterWait == null)
                    {
                        // Шторм закончился, едем нормально
                        travelTime = route.BaseTime;
                        riskAddition = 0;
                        slowdownCoefficient = 1.0;
                    }
                    else
                    {
                        // Шторм все еще продолжается - едем через него
                        var coefficients = _stormCoefficients[stormAfterWait.Severity];
                        travelTime = route.BaseTime * coefficients.slowdown;
                        riskAddition = route.BaseTime * coefficients.risk;
                        slowdownCoefficient = coefficients.slowdown;
                        stormSeverity = stormAfterWait.Severity;
                    }
                }
                else
                {
                    // Едем сразу через шторм
                    var coefficients = _stormCoefficients[activeStorm.Severity];
                    travelTime = route.BaseTime * coefficients.slowdown;
                    riskAddition = route.BaseTime * coefficients.risk;
                    slowdownCoefficient = coefficients.slowdown;
                    stormSeverity = activeStorm.Severity;
                }
            }
            else if (wait)
            {
                // Шторма нет, ожидание бессмысленно
                return null;
            }

            DateTime arrivalTime = startTime.AddHours(travelTime);
            
            // Создаем сегмент движения
            var travelSegment = new RouteSegment
            {
                Type = "Travel",
                FromNode = currentState.Node,
                ToNode = route.To,
                StartTime = startTime,
                EndTime = arrivalTime,
                Duration = travelTime,
                Risk = riskAddition,
                StormSeverity = stormSeverity,
                BaseTime = route.BaseTime,
                ActualTime = travelTime,
                SlowdownCoefficient = slowdownCoefficient
            };

            // Создаем новое состояние
            var newState = new RouteState
            {
                Node = route.To,
                CurrentTime = arrivalTime,
                TotalRisk = currentState.TotalRisk + riskAddition,
                Path = new List<string>(currentState.Path) { route.To },
                TotalTime = currentState.TotalTime + (arrivalTime - currentState.CurrentTime).TotalHours,
                TotalTravelTime = currentState.TotalTravelTime + travelTime,
                TotalWaitTime = currentState.TotalWaitTime + (waitSegment?.Duration ?? 0),
                Segments = new List<RouteSegment>(currentState.Segments)
            };

            // Добавляем сегменты в правильном порядке
            if (waitSegment != null)
            {
                newState.Segments.Add(waitSegment);
            }
            newState.Segments.Add(travelSegment);

            return newState;
        }

        private Storm? GetActiveStorm(string routeId, DateTime time)
        {
            if (_stormsByRoute.ContainsKey(routeId))
            {
                return _stormsByRoute[routeId]
                    .FirstOrDefault(storm => time >= storm.StartTime && time < storm.EndTime);
            }
            return null;
        }

        private bool IsStateWorseThanExisting(Dictionary<string, List<RouteState>> visited, RouteState newState)
        {
            if (!visited.ContainsKey(newState.Node))
                return false;

            var existingStates = visited[newState.Node];
            return existingStates.Any(existing =>
                existing.CurrentTime <= newState.CurrentTime && 
                existing.TotalRisk <= newState.TotalRisk);
        }

        private void AddToVisited(Dictionary<string, List<RouteState>> visited, RouteState state)
        {
            if (!visited.ContainsKey(state.Node))
                visited[state.Node] = new List<RouteState>();
            
            visited[state.Node].Add(state);
        }
    }

    // Простая реализация PriorityQueue для .NET
    public class PriorityQueue<TElement, TPriority> where TPriority : IComparable<TPriority>
    {
        private readonly List<(TElement Element, TPriority Priority)> _elements = new List<(TElement, TPriority)>();

        public int Count => _elements.Count;

        public void Enqueue(TElement element, TPriority priority)
        {
            _elements.Add((element, priority));
            _elements.Sort((x, y) => x.Priority.CompareTo(y.Priority));
        }

        public TElement Dequeue()
        {
            if (_elements.Count == 0)
                throw new InvalidOperationException("Queue is empty");
            
            var item = _elements[0];
            _elements.RemoveAt(0);
            return item.Element;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // Загрузка JSON файла
                string jsonFilePath = "route_data.json";
                
                if (!File.Exists(jsonFilePath))
                {
                    Console.WriteLine($"Файл {jsonFilePath} не найден!");
                    return;
                }
                
                string jsonString = File.ReadAllText(jsonFilePath);
                
                var inputData = JsonSerializer.Deserialize<InputData>(jsonString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (inputData == null)
                {
                    Console.WriteLine("Ошибка: не удалось прочитать JSON файл");
                    return;
                }

                // Инициализация алгоритма
                var router = new StormRouter();
                router.LoadData(inputData);

                // Расчет маршрутов
                var results = router.CalculateOptimalRoutes(
                    inputData.StartPoint, 
                    inputData.EndPoint, 
                    inputData.DepartureTime
                );

                // Вывод результатов
                Console.WriteLine("=== РЕЗУЛЬТАТЫ РАСЧЕТА МАРШРУТОВ ===");
                Console.WriteLine($"Отправление: {inputData.DepartureTime}");
                Console.WriteLine($"Из: {inputData.StartPoint} -> В: {inputData.EndPoint}");
                Console.WriteLine();

                if (results.Count == 0)
                {
                    Console.WriteLine("Маршрут не найден!");
                    return;
                }

                for (int i = 0; i < results.Count; i++)
                {
                    var result = results[i];
                    Console.WriteLine($"═══════════════════════════════════════════════════");
                    Console.WriteLine($"МАРШРУТ #{i + 1}");
                    Console.WriteLine($"═══════════════════════════════════════════════════");
                    Console.WriteLine($"Общая информация:");
                    Console.WriteLine($"  • Путь: {string.Join(" → ", result.Path)}");
                    Console.WriteLine($"  • Время прибытия: {result.CurrentTime}");
                    Console.WriteLine($"  • Общее время в пути: {result.TotalTime:F1} часов");
                    Console.WriteLine($"  • Время движения: {result.TotalTravelTime:F1} часов");
                    Console.WriteLine($"  • Время ожидания: {result.TotalWaitTime:F1} часов");
                    Console.WriteLine($"  • Общий риск: {result.TotalRisk:F1}");
                    Console.WriteLine();
                    
                    Console.WriteLine($"Детализация маршрута:");
                    Console.WriteLine($"---------------------------------------------------");
                    
                    for (int j = 0; j < result.Segments.Count; j++)
                    {
                        var segment = result.Segments[j];
                        Console.WriteLine($"Шаг {j + 1}:");
                        
                        if (segment.Type == "Wait")
                        {
                            Console.WriteLine($"  🕒 ОЖИДАНИЕ в узле {segment.FromNode}");
                            Console.WriteLine($"     Время: {segment.StartTime} → {segment.EndTime}");
                            Console.WriteLine($"     Продолжительность: {segment.Duration:F1} часов");
                            Console.WriteLine($"     Причина: ожидание окончания шторма");
                        }
                        else if (segment.Type == "Travel")
                        {
                            Console.WriteLine($"  🚢 ДВИЖЕНИЕ {segment.FromNode} → {segment.ToNode}");
                            Console.WriteLine($"     Время: {segment.StartTime} → {segment.EndTime}");
                            Console.WriteLine($"     Базовая продолжительность: {segment.BaseTime:F1} часов");
                            Console.WriteLine($"     Фактическая продолжительность: {segment.ActualTime:F1} часов");
                            
                            if (segment.StormSeverity != null)
                            {
                                Console.WriteLine($"     ⚠️  ШТОРМ: уровень {segment.StormSeverity}");
                                Console.WriteLine($"     Коэффициент замедления: {segment.SlowdownCoefficient:F1}");
                                Console.WriteLine($"     Добавочный риск: {segment.Risk:F1}");
                            }
                            else
                            {
                                Console.WriteLine($"     ✅ Без шторма");
                                Console.WriteLine($"     Добавочный риск: 0");
                            }
                        }
                        Console.WriteLine();
                    }
                    
                    Console.WriteLine($"Итоги маршрута #{i + 1}:");
                    Console.WriteLine($"  • Всего шагов: {result.Segments.Count}");
                    
                    var stormSegments = result.Segments.Where(s => s.StormSeverity != null).ToList();
                    if (stormSegments.Count > 0)
                    {
                        Console.WriteLine($"  • Участков со штормом: {stormSegments.Count}");
                        foreach (var stormSegment in stormSegments)
                        {
                            Console.WriteLine($"    - {stormSegment.FromNode}→{stormSegment.ToNode}: {stormSegment.StormSeverity} " +
                                            $"(+{stormSegment.Risk:F1} риска, замедление {stormSegment.SlowdownCoefficient:F1}x)");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"  • Участков со штормом: 0");
                    }
                    
                    var waitSegments = result.Segments.Where(s => s.Type == "Wait").ToList();
                    if (waitSegments.Count > 0)
                    {
                        Console.WriteLine($"  • Остановок для ожидания: {waitSegments.Count}");
                        foreach (var waitSegment in waitSegments)
                        {
                            Console.WriteLine($"    - В узле {waitSegment.FromNode}: {waitSegment.Duration:F1} часов " +
                                            $"(с {waitSegment.StartTime} по {waitSegment.EndTime})");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"  • Остановок для ожидания: 0");
                    }
                    
                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Произошла ошибка: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
            }
        }
    }
}