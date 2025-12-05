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
        private GraphVisualizer _graphVisualizer = new GraphVisualizer(); 
        private Rect _graphBounds;
        private HashSet<string> _routeNodes = new HashSet<string>();
        private TimeSpan _computationTime;
        private Random _random = new Random();
    
        private RouteReportService _reportService;

        public MainWindow()
        {
            InitializeComponent();

            _transformGroup.Children.Add(_scaleTransform);
            _transformGroup.Children.Add(_translateTransform);
            GraphCanvas.RenderTransform = _transformGroup;

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

            _nodePositions = _graphVisualizer.CalculateNodePositions(_currentData);

            AutoFitGraph();

            _graphVisualizer.Visualize(
                GraphCanvas, 
                _currentData, 
                _currentResults[0], 
                _nodePositions
            );
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
            double scale = Math.Min(scaleX, scaleY) * 0.7; 

            scale = Math.Max(0.1, Math.Min(2.0, scale));

            double offsetX = (canvasWidth - graphWidth * scale) / 2 - minX * scale + 50;
            double offsetY = (canvasHeight - graphHeight * scale) / 2 - minY * scale + 50;

            _scale = scale;
            _translateTransform.X = offsetX;
            _translateTransform.Y = offsetY;
            _scaleTransform.ScaleX = scale;
            _scaleTransform.ScaleY = scale;
        }

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