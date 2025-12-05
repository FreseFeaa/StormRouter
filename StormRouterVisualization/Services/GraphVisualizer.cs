using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using StormRouterVisualization.Models;

namespace StormRouterVisualization.Services
{
    public class GraphVisualizer
    {
        private readonly Color StartNodeColor = Color.FromRgb(56, 142, 60);     // Спокойный зеленый
        private readonly Color EndNodeColor = Color.FromRgb(211, 47, 47);       // Спокойный красный
        private readonly Color RouteNodeColor = Color.FromRgb(245, 124, 0);     // Оранжевый
        private readonly Color NormalNodeColor = Color.FromRgb(66, 133, 244);   // Спокойный синий
        private readonly Color RouteColor = Color.FromRgb(217, 48, 37);         // Ярко-красный для маршрута
        private readonly Color NormalEdgeColor = Color.FromRgb(200, 200, 200);  // Светло-серый
        
        private Dictionary<string, Point> _nodePositions = new Dictionary<string, Point>();
        private HashSet<string> _routeNodes = new HashSet<string>();
        private InputData? _currentData;  
        private RouteState? _optimalRoute; 

        public void Visualize(
            Canvas canvas, 
            InputData? data, 
            RouteState? optimalRoute, 
            Dictionary<string, Point> nodePositions)
        {
            if (data == null || optimalRoute == null) return;

            _currentData = data;
            _optimalRoute = optimalRoute;
            _nodePositions = nodePositions;
            
            canvas.Children.Clear();
            
            // Сохраняем узлы маршрута для выделения
            _routeNodes.Clear();
            foreach (var node in optimalRoute.Path)
            {
                _routeNodes.Add(node);
            }

            // Правильный порядок отрисовки
            DrawEdges(canvas);
            DrawOptimalRoute(canvas);
            DrawNodes(canvas);
        }

        private void DrawEdges(Canvas canvas)
        {
            if (_currentData?.Routes == null) return;

            double edgeOpacity = _nodePositions.Count > 30 ? 0.4 : 0.6;
            double baseThickness = _nodePositions.Count > 50 ? 0.8 : 1.2;

            foreach (var route in _currentData.Routes)
            {
                if (!_nodePositions.ContainsKey(route.From) || !_nodePositions.ContainsKey(route.To))
                    continue;

                var start = _nodePositions[route.From];
                var end = _nodePositions[route.To];

                var storm = GetStormForRoute(route.From, route.To);
                
                Brush strokeBrush;
                double strokeThickness;
                
                if (storm != null)
                {
                    strokeBrush = GetStormColor(storm.Severity);
                    strokeThickness = baseThickness * 1.8;
                }
                else
                {
                    strokeBrush = new SolidColorBrush(NormalEdgeColor);
                    strokeThickness = baseThickness;
                }

                var line = new Line
                {
                    X1 = start.X,
                    Y1 = start.Y,
                    X2 = end.X,
                    Y2 = end.Y,
                    Stroke = strokeBrush,
                    StrokeThickness = strokeThickness,
                    ToolTip = CreateEdgeTooltip(route, storm), 
                    Opacity = edgeOpacity,
                    StrokeDashArray = storm != null ? new DoubleCollection { 2, 2 } : null
                };

                canvas.Children.Add(line);

                // Время только для маленьких графов
                if (_nodePositions.Count <= 15 && storm == null)
                {
                    var textPosition = CalculateTextPosition(start, end);
                    DrawEdgeText(canvas, textPosition, $"{route.BaseTime}ч", Brushes.DarkSlateGray, 8);
                }
            }
        }

        private void DrawNodes(Canvas canvas)
        {
            double baseNodeSize = _nodePositions.Count > 50 ? 24 : 
                                 _nodePositions.Count > 20 ? 30 : 36;
            double baseFontSize = _nodePositions.Count > 50 ? 8 : 
                                 _nodePositions.Count > 20 ? 9 : 11;

            foreach (var (nodeName, position) in _nodePositions)
            {
                Brush nodeColor = GetNodeColor(nodeName);
                
                var ellipse = new Ellipse
                {
                    Width = baseNodeSize,
                    Height = baseNodeSize,
                    Fill = CreateNodeGradient(nodeColor),
                    Stroke = Brushes.White,
                    StrokeThickness = 1.5,
                    ToolTip = CreateNodeTooltip(nodeName),
                    Cursor = Cursors.Hand 
                };

                // Добавляем эффект тени
                ellipse.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 320,
                    ShadowDepth = 2,
                    Opacity = 0.3,
                    BlurRadius = 3
                };

                Canvas.SetLeft(ellipse, position.X - baseNodeSize / 2);
                Canvas.SetTop(ellipse, position.Y - baseNodeSize / 2);

                canvas.Children.Add(ellipse);

                // Подписываем узлы
                DrawNodeText(canvas, position, nodeName, Brushes.White, baseFontSize, baseNodeSize);
            }
        }

        private void DrawOptimalRoute(Canvas canvas)
        {
            if (_optimalRoute == null || _optimalRoute.Path.Count < 2) return;

            for (int i = 0; i < _optimalRoute.Path.Count - 1; i++)
            {
                var from = _optimalRoute.Path[i];
                var to = _optimalRoute.Path[i + 1];

                if (!_nodePositions.ContainsKey(from) || !_nodePositions.ContainsKey(to))
                    continue;

                var start = _nodePositions[from];
                var end = _nodePositions[to];

                var line = new Line
                {
                    X1 = start.X,
                    Y1 = start.Y,
                    X2 = end.X,
                    Y2 = end.Y,
                    Stroke = new SolidColorBrush(RouteColor),
                    StrokeThickness = _nodePositions.Count > 50 ? 4 : 5,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Opacity = 0.9
                };

                canvas.Children.Add(line);

                if (_nodePositions.Count <= 30)
                {
                    DrawDirectionArrow(canvas, start, end);
                }
            }
        }

        private void DrawDirectionArrow(Canvas canvas, Point start, Point end)
        {
            Vector direction = end - start;
            if (direction.Length > 0)
            {
                direction.Normalize();
            }

            Vector perpendicular = new Vector(-direction.Y, direction.X);
            double arrowSize = 8;

            Point arrowPoint1 = end - direction * arrowSize + perpendicular * arrowSize / 2;
            Point arrowPoint2 = end - direction * arrowSize - perpendicular * arrowSize / 2;

            var arrow = new Polygon
            {
                Points = new PointCollection { end, arrowPoint1, arrowPoint2 },
                Fill = new SolidColorBrush(RouteColor),
                Stroke = new SolidColorBrush(RouteColor),
                StrokeThickness = 1
            };

            canvas.Children.Add(arrow);
        }

        private void DrawNodeText(Canvas canvas, Point position, string text, Brush color, double fontSize, double nodeSize)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                Foreground = color,
                FontSize = fontSize,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 40, 40, 40)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1, 4, 1),
                Child = textBlock
            };

            border.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            
            Canvas.SetLeft(border, position.X - border.DesiredSize.Width / 2);
            Canvas.SetTop(border, position.Y - border.DesiredSize.Height / 2);

            canvas.Children.Add(border);
        }

        private void DrawEdgeText(Canvas canvas, Point position, string text, Brush color, double fontSize)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(150, 200, 200, 200)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Padding = new Thickness(3, 1, 3, 1),
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = color,
                    FontSize = fontSize,
                    FontWeight = FontWeights.Normal
                }
            };

            border.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            
            Canvas.SetLeft(border, position.X - border.DesiredSize.Width / 2);
            Canvas.SetTop(border, position.Y - border.DesiredSize.Height / 2);

            canvas.Children.Add(border);
        }

        private Point CalculateTextPosition(Point start, Point end)
        {
            Vector direction = end - start;
            if (direction.Length > 0)
            {
                direction.Normalize();
            }
            
            Vector perpendicular = new Vector(-direction.Y, direction.X);
            perpendicular.Normalize();
            
            Point center = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
            return new Point(center.X + perpendicular.X * 10, center.Y + perpendicular.Y * 10);
        }

        private Storm? GetStormForRoute(string from, string to)
        {
            var routeId = $"{from}-{to}";
            return _currentData?.Storms?.FirstOrDefault(s => s.RouteId == routeId);
        }

        private Brush GetStormColor(string? severity) 
        {
            if (string.IsNullOrEmpty(severity))
                return new SolidColorBrush(NormalEdgeColor);

            return severity.ToLower() switch
            {
                "low" => new SolidColorBrush(Color.FromRgb(255, 213, 79)),
                "medium" => new SolidColorBrush(Color.FromRgb(255, 167, 38)),
                "high" => new SolidColorBrush(Color.FromRgb(255, 87, 34)),
                _ => new SolidColorBrush(NormalEdgeColor)
            };
        }

        private Brush GetNodeColor(string nodeName)
        {
            if (nodeName == _currentData?.StartPoint)
                return new SolidColorBrush(StartNodeColor);
            else if (nodeName == _currentData?.EndPoint)
                return new SolidColorBrush(EndNodeColor);
            else if (_routeNodes.Contains(nodeName))
                return new SolidColorBrush(RouteNodeColor);
            else
                return new SolidColorBrush(NormalNodeColor);
        }

        private Brush CreateNodeGradient(Brush baseColor)
        {
            if (baseColor is SolidColorBrush solidBrush)
            {
                Color baseColorValue = solidBrush.Color;
                Color lighterColor = Color.FromArgb(255, 
                    (byte)Math.Min(255, baseColorValue.R + 40),
                    (byte)Math.Min(255, baseColorValue.G + 40),
                    (byte)Math.Min(255, baseColorValue.B + 40));

                var gradient = new RadialGradientBrush
                {
                    GradientOrigin = new Point(0.3, 0.3),
                    Center = new Point(0.3, 0.3),
                    RadiusX = 0.8,
                    RadiusY = 0.8,
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(lighterColor, 0.0),
                        new GradientStop(baseColorValue, 1.0)
                    }
                };
                return gradient;
            }
            return baseColor;
        }

        private object CreateEdgeTooltip(Route route, Storm? storm)
        {
            var tooltip = $"Маршрут: {route.From} → {route.To}\n" +
                         $"Расстояние: {route.Distance}\n" +
                         $"Базовое время: {route.BaseTime}ч";

            if (storm != null)
            {
                var (slowdown, risk) = GetStormCoefficients(storm.Severity);
                tooltip += $"\n\n⚡ ШТОРМ\n" +
                          $"Уровень: {storm.Severity}\n" +
                          $"Замедление: {slowdown:F1}x\n" +
                          $"Риск: {risk}\n" +
                          $"Время: {storm.StartTime:dd.MM HH:mm} - {storm.EndTime:dd.MM HH:mm}";
            }

            return tooltip;
        }

        private object CreateNodeTooltip(string nodeName)
        {
            var tooltip = $"Узел: {nodeName}";
            
            if (nodeName == _currentData?.StartPoint)
                tooltip += " 🟢 (Старт)";
            else if (nodeName == _currentData?.EndPoint)
                tooltip += " 🔴 (Финиш)";
            else if (_routeNodes.Contains(nodeName))
                tooltip += " 🟠 (В маршруте)";
                
            return tooltip;
        }

        public static (double slowdown, int risk) GetStormCoefficients(string? severity) 
        {
            if (string.IsNullOrEmpty(severity))
                return (1.0, 0);

            return severity.ToLower() switch
            {
                "low" => (1.2, 20),
                "medium" => (1.5, 40),
                "high" => (2.0, 60),
                _ => (1.0, 0)
            };
        }
        public Dictionary<string, Point> CalculateNodePositions(InputData data)
        {
            var nodes = GetAllNodes(data);
            if (nodes.Count <= 8)
                return CalculateStableCircularLayout(nodes, 200);
            else if (nodes.Count <= 30)
                return CalculateEnhancedForceDirectedLayout(nodes, data.Routes);
            else
                return CalculateHierarchicalLayout(nodes, data.Routes, data.StartPoint);
        }

        private List<string> GetAllNodes(InputData data)
        {
            return data.Routes
                    .SelectMany(r => new[] { r.From, r.To })
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();
        }

        private Dictionary<string, Point> CalculateStableCircularLayout(List<string> nodes, double radius)
        {
            var positions = new Dictionary<string, Point>();
            double centerX = 400;
            double centerY = 300;

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
            double repulsionForce = 150000 / nodes.Count;
            double attractionForce = 0.1;
            double idealLength = 120;
            int iterations = 150;
            double damping = 0.9;

            var connections = nodes.ToDictionary(n => n, n => new List<string>());
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
                var forces = nodes.ToDictionary(n => n, n => new Vector(0, 0));

                // Отталкивание
                for (int i = 0; i < nodes.Count; i++)
                {
                    for (int j = i + 1; j < nodes.Count; j++)
                    {
                        var delta = positions[nodes[i]] - positions[nodes[j]];
                        double distance = Math.Max(delta.Length, 0.1);
                        var force = delta / distance * (repulsionForce / (distance * distance));
                        forces[nodes[i]] += force;
                        forces[nodes[j]] -= force;
                    }
                }

                // Притяжение
                foreach (var route in routes)
                {
                    if (!positions.ContainsKey(route.From) || !positions.ContainsKey(route.To)) continue;

                    var delta = positions[route.To] - positions[route.From];
                    double distance = Math.Max(delta.Length, 0.1);
                    var force = delta / distance * (attractionForce * (distance - idealLength));
                    forces[route.From] += force;
                    forces[route.To] -= force;
                }

                foreach (var node in nodes)
                {
                    var force = forces[node];
                    if (force.Length > 8) force = force / force.Length * 8;
                    positions[node] = new Point(
                        positions[node].X + force.X * damping,
                        positions[node].Y + force.Y * damping
                    );

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

        private Dictionary<string, Point> CalculateHierarchicalLayout(List<string> nodes, List<Route> routes, string? startPoint)
        {
            var positions = new Dictionary<string, Point>();
            var levels = nodes.ToDictionary(n => n, n => -1);
            var queue = new Queue<string>();

            if (!string.IsNullOrEmpty(startPoint) && nodes.Contains(startPoint))
            {
                levels[startPoint] = 0;
                queue.Enqueue(startPoint);
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var neighbors = routes.Where(r => r.From == current).Select(r => r.To)
                                    .Concat(routes.Where(r => r.To == current).Select(r => r.From))
                                    .Distinct();

                foreach (var neighbor in neighbors)
                {
                    if (levels[neighbor] == -1)
                    {
                        levels[neighbor] = levels[current] + 1;
                        queue.Enqueue(neighbor);
                    }
                }
            }

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
    }
}