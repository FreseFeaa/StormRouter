using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using StormRouterVisualization.Models;
using StormRouterVisualization.Services;

namespace StormRouterVisualization
{
    public partial class MainWindow : Window
    {
        private InputData? _currentData;
        private List<RouteState>? _currentResults;
        private Point _lastMousePosition;
        private double _scale = 1.0;
        private const double ScaleRate = 1.1;
        private TranslateTransform _translateTransform = new TranslateTransform();
        private ScaleTransform _scaleTransform = new ScaleTransform();
        private TransformGroup _transformGroup = new TransformGroup();
        private bool _isDragging = false;
        private Dictionary<string, Point> _nodePositions = new Dictionary<string, Point>();
        private GraphVisualizer _graphVisualizer = new GraphVisualizer(); // <-- Добавить
        private Rect _graphBounds;
        private HashSet<string> _routeNodes = new HashSet<string>();
        private TimeSpan _computationTime;
        private Random _random = new Random();
    
        private RouteReportService _reportService;


        // цветовая схема
        private readonly Color StartNodeColor = Color.FromRgb(56, 142, 60);     // Спокойный зеленый
        private readonly Color EndNodeColor = Color.FromRgb(211, 47, 47);       // Спокойный красный
        private readonly Color RouteNodeColor = Color.FromRgb(245, 124, 0);     // Оранжевый
        private readonly Color NormalNodeColor = Color.FromRgb(66, 133, 244);   // Спокойный синий
        private readonly Color RouteColor = Color.FromRgb(217, 48, 37);         // Ярко-красный для маршрута
        private readonly Color NormalEdgeColor = Color.FromRgb(200, 200, 200);  // Светло-серый

        public MainWindow()
        {
            InitializeComponent();

            _transformGroup.Children.Add(_scaleTransform);
            _transformGroup.Children.Add(_translateTransform);
            GraphCanvas.RenderTransform = _transformGroup;

            // Инициализация сервиса перед генерацией графа
            var styles = new Dictionary<string, Style>();
            if (TryFindResource("StatTextBlock") is Style statStyle)
                styles["StatTextBlock"] = statStyle;
            _reportService = new RouteReportService(styles);

            // Генерируем случайный граф
            GenerateRandomGraph();
        }


        private void LoadJsonButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    DefaultExt = ".json",
                    Title = "Выберите файл с данными маршрутов"
                };

                if (dialog.ShowDialog() == true)
                {
                    LoadAndProcessJsonFile(dialog.FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке файла:\n{ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GenerateRandomGraphButton_Click(object sender, RoutedEventArgs e)
        {
            GenerateRandomGraph();
        }

        private void GenerateRandomGraph()
        {
            try
            {
                var generator = new RandomGraphGenerator();
                var randomData = generator.Generate(); 

                if (randomData != null)
                {
                    LoadInputData(randomData, "Случайный граф");
                    StatusText.Text = "Сгенерирован случайный граф";
                }
                else
                {
                    MessageBox.Show(
                        "Не удалось сгенерировать граф с валидным маршрутом. Попробуйте еще раз.", 
                        "Информация", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Information
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при генерации графа:\n{ex.Message}", 
                    "Ошибка", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error
                );
            }
        }

        private void LoadAndProcessJsonFile(string filePath)
        {
            try
            {
                string jsonString = File.ReadAllText(filePath);
                var inputData = JsonSerializer.Deserialize<InputData>(jsonString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (inputData == null)
                {
                    MessageBox.Show("Ошибка: Не удалось прочитать JSON файл или файл пуст", "Ошибка загрузки", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (inputData.Routes == null || inputData.Routes.Count == 0)
                {
                    MessageBox.Show("Ошибка: В файле отсутствуют данные о маршрутах", "Ошибка загрузки", 
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                LoadInputData(inputData, System.IO.Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке файла:\n{ex.Message}", "Ошибка загрузки", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadInputData(InputData inputData, string sourceName)
                {
                    ResetView();
                    GraphCanvas.Children.Clear();

                    _currentData = inputData;
                    StatusText.Text = $"Загружен: {sourceName}";

                    var router = new StormRouter();
                    router.LoadData(_currentData);

                    var stopwatch = Stopwatch.StartNew();
                    _currentResults = router.CalculateOptimalRoutes(
                        _currentData.StartPoint,
                        _currentData.EndPoint,
                        _currentData.DepartureTime
                    );
                    stopwatch.Stop();
                    _computationTime = stopwatch.Elapsed;

                    VisualizeGraph();

                    RouteDetailsText.Text = _reportService.GenerateRouteDetails(_currentData, _currentResults);
                    _reportService.PopulateStatisticsPanel(StatsPanel, _currentData, _currentResults, _nodePositions, _computationTime);
                    RawDataText.Text = _reportService.FormatRawData(JsonSerializer.Serialize(_currentData, new JsonSerializerOptions { WriteIndented = true }));

                    InfoTabControl.SelectedIndex = 1;
                }
        private void VisualizeGraph()
        {
            if (_currentData == null || _currentResults == null || _currentResults.Count == 0) 
                return;

            GraphCanvas.Children.Clear();
            _nodePositions = CalculateNodePositions();
            AutoFitGraph();

            // Используем визуализатор
            _graphVisualizer.Visualize(
                GraphCanvas, 
                _currentData, 
                _currentResults[0], 
                _nodePositions
            );
        }
        private Dictionary<string, Point> CalculateNodePositions()
        {
            var positions = new Dictionary<string, Point>();
            var nodes = GetAllNodes();

            if (nodes.Count == 0) return positions;

            // Для маленьких графов - стабильное круговое расположение
            if (nodes.Count <= 8)
            {
                return CalculateStableCircularLayout(nodes, 200);
            }
            // Для средних графов - улучшенный силовой алгоритм
            else if (nodes.Count <= 30)
            {
                return CalculateEnhancedForceDirectedLayout(nodes, _currentData?.Routes ?? new List<Route>());
            }
            // Для больших графов - иерархическое расположение
            else
            {
                return CalculateHierarchicalLayout(nodes, _currentData?.Routes ?? new List<Route>());
            }
        }

        private Dictionary<string, Point> CalculateStableCircularLayout(List<string> nodes, double radius)
        {
            var positions = new Dictionary<string, Point>();
            double centerX = 400;
            double centerY = 300;

            // Сортируем узлы для стабильного расположения
            var sortedNodes = nodes.OrderBy(n => n).ToList();

            double angleStep = 2 * Math.PI / sortedNodes.Count;

            for (int i = 0; i < sortedNodes.Count; i++)
            {
                double angle = i * angleStep;
                double x = centerX + radius * Math.Cos(angle);
                double y = centerY + radius * Math.Sin(angle);
                positions[sortedNodes[i]] = new Point(x, y);
            }

            return positions;
        }

        private Dictionary<string, Point> CalculateEnhancedForceDirectedLayout(List<string> nodes, List<Route> routes)
        {
            var positions = CalculateStableCircularLayout(nodes, Math.Max(250, nodes.Count * 8));
            
            // Параметры алгоритма
            double repulsionForce = 150000 / nodes.Count;
            double attractionForce = 0.1;
            double idealLength = 120;
            int iterations = 150;
            double damping = 0.9;

            var connections = new Dictionary<string, List<string>>();
            foreach (var node in nodes)
            {
                connections[node] = new List<string>();
            }
            
            foreach (var route in routes)
            {
                if (connections.ContainsKey(route.From) && connections.ContainsKey(route.To))
                {
                    connections[route.From].Add(route.To);
                    if (!connections[route.To].Contains(route.From))
                        connections[route.To].Add(route.From);
                }
            }

            for (int iter = 0; iter < iterations; iter++)
            {
                var forces = new Dictionary<string, Vector>();
                
                foreach (var node in nodes)
                {
                    forces[node] = new Vector(0, 0);
                }

                // Силы отталкивания
                for (int i = 0; i < nodes.Count; i++)
                {
                    for (int j = i + 1; j < nodes.Count; j++)
                    {
                        var node1 = nodes[i];
                        var node2 = nodes[j];
                        
                        var delta = positions[node1] - positions[node2];
                        double distance = Math.Max(delta.Length, 0.1);
                        
                        double force = repulsionForce / (distance * distance);
                        var forceVector = new Vector(
                            delta.X / distance * force,
                            delta.Y / distance * force
                        );
                        
                        forces[node1] += forceVector;
                        forces[node2] -= forceVector;
                    }
                }

                // Силы притяжения для связанных узлов
                foreach (var route in routes)
                {
                    if (!positions.ContainsKey(route.From) || !positions.ContainsKey(route.To))
                        continue;

                    var fromPos = positions[route.From];
                    var toPos = positions[route.To];
                    
                    var delta = toPos - fromPos;
                    double distance = Math.Max(delta.Length, 0.1);
                    
                    double force = attractionForce * (distance - idealLength);
                    var forceVector = new Vector(
                        delta.X / distance * force,
                        delta.Y / distance * force
                    );
                    
                    forces[route.From] += forceVector;
                    forces[route.To] -= forceVector;
                }

                // Применение сил
                foreach (var node in nodes)
                {
                    var force = forces[node];
                    
                    double maxForce = 8;
                    if (force.Length > maxForce)
                    {
                        force = force / force.Length * maxForce;
                    }
                    
                    positions[node] = new Point(
                        positions[node].X + force.X * damping,
                        positions[node].Y + force.Y * damping
                    );
                    
                    // Ограничение области
                    double margin = 80;
                    positions[node] = new Point(
                        Math.Max(margin, Math.Min(720, positions[node].X)),
                        Math.Max(margin, Math.Min(520, positions[node].Y))
                    );
                }

                damping *= 0.98;
            }

            return positions;
        }

        private Dictionary<string, Point> CalculateHierarchicalLayout(List<string> nodes, List<Route> routes)
        {
            var positions = new Dictionary<string, Point>();
            
            // Разбиваем на уровни по удаленности от стартовой точки
            var levels = new Dictionary<string, int>();
            var queue = new Queue<string>();
            
            foreach (var node in nodes)
            {
                levels[node] = -1;
            }
            
            if (_currentData != null && nodes.Contains(_currentData.StartPoint))
            {
                levels[_currentData.StartPoint] = 0;
                queue.Enqueue(_currentData.StartPoint);
            }

            // BFS для определения уровней
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var connectedNodes = routes.Where(r => r.From == current).Select(r => r.To)
                    .Concat(routes.Where(r => r.To == current).Select(r => r.From))
                    .Distinct();

                foreach (var neighbor in connectedNodes)
                {
                    if (levels[neighbor] == -1)
                    {
                        levels[neighbor] = levels[current] + 1;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // Располагаем узлы по уровням
            var nodesByLevel = nodes.GroupBy(n => Math.Max(0, levels[n]))
                                  .OrderBy(g => g.Key)
                                  .ToList();

            double startX = 100;
            double startY = 100;
            double levelSpacing = 500.0 / Math.Max(1, nodesByLevel.Count);

            foreach (var levelGroup in nodesByLevel)
            {
                var levelNodes = levelGroup.OrderBy(n => n).ToList();
                double y = startY + levelGroup.Key * levelSpacing;
                
                for (int i = 0; i < levelNodes.Count; i++)
                {
                    double x = startX + (600.0 / (levelNodes.Count + 1)) * (i + 1);
                    positions[levelNodes[i]] = new Point(x, y);
                }
            }

            return positions;
        }

        private void AutoFitGraph()
        {
            if (_nodePositions.Count == 0) return;

            double minX = _nodePositions.Values.Min(p => p.X);
            double maxX = _nodePositions.Values.Max(p => p.X);
            double minY = _nodePositions.Values.Min(p => p.Y);
            double maxY = _nodePositions.Values.Max(p => p.Y);

            _graphBounds = new Rect(minX, minY, maxX - minX, maxY - minY);

            double graphWidth = _graphBounds.Width;
            double graphHeight = _graphBounds.Height;

            if (graphWidth == 0 || graphHeight == 0) return;

            double canvasWidth = GraphCanvas.ActualWidth > 0 ? GraphCanvas.ActualWidth : 800;
            double canvasHeight = GraphCanvas.ActualHeight > 0 ? GraphCanvas.ActualHeight : 600;

            double scaleX = canvasWidth / graphWidth;
            double scaleY = canvasHeight / graphHeight;
            double scale = Math.Min(scaleX, scaleY) * 0.7; // Уменьшил коэффициент для лучшего fit

            scale = Math.Max(0.1, Math.Min(2.0, scale));

            double offsetX = (canvasWidth - graphWidth * scale) / 2 - minX * scale + 50;
            double offsetY = (canvasHeight - graphHeight * scale) / 2 - minY * scale + 50;

            _scale = scale;
            _translateTransform.X = offsetX;
            _translateTransform.Y = offsetY;
            _scaleTransform.ScaleX = scale;
            _scaleTransform.ScaleY = scale;
        }

        private List<string> GetAllNodes()
        {
            var nodes = new HashSet<string>();
            
            if (_currentData?.Routes != null)
            {
                foreach (var route in _currentData.Routes)
                {
                    nodes.Add(route.From);
                    nodes.Add(route.To);
                }
            }
            
            return nodes.OrderBy(n => n).ToList();
        }

        // private (double slowdown, int risk) GetStormCoefficients(string severity)
        // {
        //     if (string.IsNullOrEmpty(severity))
        //         return (1.0, 0);

        //     return severity.ToLower() switch
        //     {
        //         "low" => (1.2, 20),
        //         "medium" => (1.5, 40),
        //         "high" => (2.0, 60),
        //         _ => (1.0, 0)
        //     };
        // }

        private void GraphCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var zoomCenter = e.GetPosition(GraphCanvas);
            
            double zoomFactor = e.Delta > 0 ? ScaleRate : 1 / ScaleRate;
            double newScale = _scale * zoomFactor;
            
            newScale = Math.Max(0.1, Math.Min(5.0, newScale));
            
            double scaleChange = newScale / _scale;
            
            _scale = newScale;
            _scaleTransform.ScaleX = _scale;
            _scaleTransform.ScaleY = _scale;
            
            _translateTransform.X = zoomCenter.X - (zoomCenter.X - _translateTransform.X) * scaleChange;
            _translateTransform.Y = zoomCenter.Y - (zoomCenter.Y - _translateTransform.Y) * scaleChange;
            
            LimitTranslation();
            
            e.Handled = true;
        }

        private void GraphCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _lastMousePosition = e.GetPosition(GraphCanvas);
            _isDragging = true;
            GraphCanvas.CaptureMouse();
            GraphCanvas.Cursor = Cursors.SizeAll;
        }

        private void GraphCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPosition = e.GetPosition(GraphCanvas);
                _translateTransform.X += currentPosition.X - _lastMousePosition.X;
                _translateTransform.Y += currentPosition.Y - _lastMousePosition.Y;
                _lastMousePosition = currentPosition;
                
                LimitTranslation();
            }
        }

        private void LimitTranslation()
        {
            double maxOffset = 1000;
            
            _translateTransform.X = Math.Max(-maxOffset, Math.Min(maxOffset, _translateTransform.X));
            _translateTransform.Y = Math.Max(-maxOffset, Math.Min(maxOffset, _translateTransform.Y));
        }

        private void GraphCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            GraphCanvas.ReleaseMouseCapture();
            GraphCanvas.Cursor = Cursors.Arrow;
        }

        private void ResetViewButton_Click(object sender, RoutedEventArgs e)
        {
            ResetView();
        }

        private void ResetView()
        {
            _scale = 1.0;
            _translateTransform.X = 0;
            _translateTransform.Y = 0;
            _scaleTransform.ScaleX = 1.0;
            _scaleTransform.ScaleY = 1.0;
            
            if (_currentData != null)
            {
                AutoFitGraph();
            }
        }
    }
}