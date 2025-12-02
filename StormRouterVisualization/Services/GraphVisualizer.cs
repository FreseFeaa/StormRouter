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
        // Добавляем using System.Windows.Input для Cursors
        private readonly Color StartNodeColor = Color.FromRgb(56, 142, 60);
        private readonly Color EndNodeColor = Color.FromRgb(211, 47, 47);
        private readonly Color RouteNodeColor = Color.FromRgb(245, 124, 0);
        private readonly Color NormalNodeColor = Color.FromRgb(66, 133, 244);
        private readonly Color RouteColor = Color.FromRgb(217, 48, 37);
        private readonly Color NormalEdgeColor = Color.FromRgb(200, 200, 200);
        
        private Dictionary<string, Point> _nodePositions = new Dictionary<string, Point>();
        private HashSet<string> _routeNodes = new HashSet<string>();
        private InputData? _currentData;  // Добавляем nullable
        private RouteState? _optimalRoute; // Добавляем nullable

        public void Visualize(
            Canvas canvas, 
            InputData? data,  // Делаем nullable
            RouteState? optimalRoute, // Делаем nullable
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
                    ToolTip = CreateEdgeTooltip(route, storm), // Исправляем предупреждение
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
                    Cursor = Cursors.Hand // Исправлено - добавили using System.Windows.Input
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

        private Brush GetStormColor(string? severity) // Добавляем nullable
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

        private object CreateEdgeTooltip(Route route, Storm? storm) // Добавляем nullable
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

        public static (double slowdown, int risk) GetStormCoefficients(string? severity) // Добавляем nullable
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
    }
}