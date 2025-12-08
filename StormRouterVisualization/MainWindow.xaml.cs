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
using Microsoft.Win32;
using StormBase.Models;
using StormBase.Services;
using StormRouterVisualization.Services;

namespace StormRouterVisualization
{
    public partial class MainWindow : Window
    {
        private InputData? _currentData;
        private List<RouteState>? _currentResults;

        private readonly GraphVisualizer _graphVisualizer = new GraphVisualizer();
        private readonly GraphInteractionService _interaction = new GraphInteractionService();

        private Dictionary<string, Point> _nodePositions = new();
        private TimeSpan _computationTime;

        private readonly RouteReportService _reportService;

        public MainWindow()
        {
            InitializeComponent();

            var group = new TransformGroup();
            group.Children.Add(_interaction.ScaleTransform);
            group.Children.Add(_interaction.Translate);
            GraphCanvas.RenderTransform = group;

            var styles = new Dictionary<string, Style>();
            if (TryFindResource("StatTextBlock") is Style statStyle)
                styles["StatTextBlock"] = statStyle;
            _reportService = new RouteReportService(styles);

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
                    Title = "Выберите файл"
                };

                if (dialog.ShowDialog() == true)
                    LoadAndProcessJsonFile(dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    StatusText.Text = "Случайный граф создан";
                }
                else
                {
                    MessageBox.Show("Не удалось создать граф с маршрутом", "Инфо", MessageBoxButton.OK);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка генерации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadAndProcessJsonFile(string filePath)
        {
            try
            {
                string jsonString = File.ReadAllText(filePath);
                var inputData = JsonSerializer.Deserialize<InputData>(jsonString,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                if (inputData == null)
                {
                    MessageBox.Show("JSON пуст или некорректен", "Ошибка", MessageBoxButton.OK);
                    return;
                }

                LoadInputData(inputData, Path.GetFileName(filePath));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка JSON: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadInputData(InputData inputData, string source)
        {
            ResetView();
            GraphCanvas.Children.Clear();

            _currentData = inputData;
            StatusText.Text = $"Загружен: {source}";

            var router = new StormRouter();
            router.LoadData(_currentData);

            var sw = Stopwatch.StartNew();
            _currentResults = router.CalculateOptimalRoutes(
                _currentData.StartPoint,
                _currentData.EndPoint,
                _currentData.DepartureTime
            );
            sw.Stop();
            _computationTime = sw.Elapsed;

            VisualizeGraph();

            RouteDetailsText.Text = _reportService.GenerateRouteDetails(_currentData, _currentResults);
            _reportService.PopulateStatisticsPanel(StatsPanel, _currentData, _currentResults, _nodePositions, _computationTime);
            RawDataText.Text = _reportService.FormatRawData(
                JsonSerializer.Serialize(_currentData, new JsonSerializerOptions { WriteIndented = true })
            );

            InfoTabControl.SelectedIndex = 1;
        }

        private void VisualizeGraph()
        {
            if (_currentData == null ||
                _currentResults == null ||
                _currentResults.Count == 0)
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

            double graphWidth = maxX - minX;
            double graphHeight = maxY - minY;

            double canvasWidth = GraphCanvas.ActualWidth > 0 ? GraphCanvas.ActualWidth : 800;
            double canvasHeight = GraphCanvas.ActualHeight > 0 ? GraphCanvas.ActualHeight : 600;

            double scaleX = canvasWidth / graphWidth;
            double scaleY = canvasHeight / graphHeight;
            double scale = Math.Min(scaleX, scaleY) * 0.7;

            scale = Math.Clamp(scale, 0.1, 2.0);

            double offsetX = (canvasWidth - graphWidth * scale) / 2 - minX * scale + 50;
            double offsetY = (canvasHeight - graphHeight * scale) / 2 - minY * scale + 50;

            _interaction.ScaleTransform.ScaleX = scale;
            _interaction.ScaleTransform.ScaleY = scale;

            _interaction.Translate.X = offsetX;
            _interaction.Translate.Y = offsetY;
        }

        private void GraphCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            _interaction.Zoom(e.Delta, e.GetPosition(GraphCanvas));
        }

        private void GraphCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _interaction.BeginDrag(e.GetPosition(GraphCanvas));
            GraphCanvas.CaptureMouse();
            GraphCanvas.Cursor = Cursors.SizeAll;
        }

        private void GraphCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                _interaction.Drag(e.GetPosition(GraphCanvas));
        }

        private void GraphCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _interaction.EndDrag();
            GraphCanvas.ReleaseMouseCapture();
            GraphCanvas.Cursor = Cursors.Arrow;
        }

        private void ResetViewButton_Click(object sender, RoutedEventArgs e)
        {
            ResetView();
        }

        private void ResetView()
        {
            _interaction.Reset();

            if (_currentData != null)
                AutoFitGraph();
        }
    }
}
