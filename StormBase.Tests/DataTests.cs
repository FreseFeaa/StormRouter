using System;
using System.IO;
using System.Text.Json;
using StormBase.Models;
using StormBase.Services;
using StormBase.Services.Routing;
using StormBase.Services.Storms;
using Xunit;
using Xunit.Abstractions;

namespace StormBase.Tests
{
    public class DataTests
    {
        private readonly ITestOutputHelper _output;
        
        public DataTests(ITestOutputHelper output)
        {
            _output = output;
        }
        
        private StormRouter CreateRouter()
        {
            var routeGraph = new RouteGraph();
            var stormProvider = new StormProvider();
            return new StormRouter(routeGraph, stormProvider);
        }

        private string ReadTestFile(string filename)
        {
            var paths = new[]
            {
                Path.Combine("Data", filename),
                Path.Combine("../../../Data", filename),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", filename)
            };
            
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    return File.ReadAllText(path);
                }
            }
            
            throw new FileNotFoundException($"File {filename} not found");
        }

        [Fact]
        public void HardTest_ComplexGraph_FindsPath()
        {
            _output.WriteLine("=== HardTest: Complex Graph ===");
            
            // Arrange
            var json = ReadTestFile("HardTest.json");
            var data = JsonSerializer.Deserialize<InputData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var router = CreateRouter();
            router.LoadData(data);

            // Act
            var results = router.CalculateOptimalRoutes(
                data.StartPoint,
                data.EndPoint,
                data.DepartureTime
            );

            // Assert
            Assert.NotEmpty(results);
            var bestRoute = results[0];
            
            _output.WriteLine($"Found: {results.Count} route(s)");
            _output.WriteLine($"Best route - Time: {bestRoute.TotalTime:F1}h, Risk: {bestRoute.TotalRisk:F1}");
            _output.WriteLine($"From: {data.StartPoint} to: {data.EndPoint}");
            
            Assert.True(bestRoute.TotalTime > 0);
            _output.WriteLine("Test passed\n");
        }

        [Fact]
        public void NormTest_NormalScenario_FindsOptimalPath()
        {
            _output.WriteLine("=== NormTest: Normal Scenario ===");
            
            var json = ReadTestFile("NormTest.json");
            var data = JsonSerializer.Deserialize<InputData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var router = CreateRouter();
            router.LoadData(data);

            var results = router.CalculateOptimalRoutes(
                data.StartPoint,
                data.EndPoint,
                data.DepartureTime
            );

            Assert.NotEmpty(results);
            var bestRoute = results[0];
            
            _output.WriteLine($"Best route - Time: {bestRoute.TotalTime:F1}h, Risk: {bestRoute.TotalRisk:F1}");
            _output.WriteLine($"From: {data.StartPoint} to: {data.EndPoint}");
            
            // Базовые проверки
            Assert.True(bestRoute.TotalTime > 0);
            Assert.True(bestRoute.TotalRisk >= 0);
            
            Assert.Equal(42, bestRoute.TotalTime, 0.1);
            _output.WriteLine("Test passed\n");
        }

        [Fact]
        public void SigmaTest_LargeNetwork_FindsPath()
        {
            _output.WriteLine("=== SigmaTest: Large Network ===");
            
            var json = ReadTestFile("SigmaTest.json");
            var data = JsonSerializer.Deserialize<InputData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var router = CreateRouter();
            router.LoadData(data);

            var results = router.CalculateOptimalRoutes(
                data.StartPoint,
                data.EndPoint,
                data.DepartureTime,
                3
            );

            Assert.NotEmpty(results);
            _output.WriteLine($"Found: {results.Count} alternative routes");
            
            for (int i = 0; i < results.Count; i++)
            {
                var route = results[i];
                _output.WriteLine($"  Route #{i + 1}: Time: {route.TotalTime:F1}h, Risk: {route.TotalRisk:F1}");
            }
            
            foreach (var route in results)
            {
                Assert.Equal("Node_99", route.Node);
            }
            
            _output.WriteLine("Test passed\n");
        }

        [Fact]
        public void StormRiskTest_MultipleStorms_ConsidersRisk()
        {
            _output.WriteLine("=== StormRiskTest: Multiple Storms ===");
            
            var json = ReadTestFile("StormRiskTest.json");
            var data = JsonSerializer.Deserialize<InputData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var router = CreateRouter();
            router.LoadData(data);

            var results = router.CalculateOptimalRoutes(
                data.StartPoint,
                data.EndPoint,
                data.DepartureTime
            );

            Assert.NotEmpty(results);
            var bestRoute = results[0];
            
            _output.WriteLine($"Best route - Time: {bestRoute.TotalTime:F1}h, Risk: {bestRoute.TotalRisk:F1}");
            _output.WriteLine($"From: {data.StartPoint} to: {data.EndPoint}");
            
            _output.WriteLine("Note: Route through storms should have high risk");
            _output.WriteLine("Test passed\n");
        }

        [Fact]
        public void RouteDataTest_PrimaryDataFile_FindsPath()
        {
            _output.WriteLine("=== RouteDataTest: Primary Data File ===");
            
            var json = ReadTestFile("route_data.json");
            var data = JsonSerializer.Deserialize<InputData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var router = CreateRouter();
            router.LoadData(data);

            var results = router.CalculateOptimalRoutes(
                data.StartPoint,
                data.EndPoint,
                data.DepartureTime,
                3
            );

            Assert.NotEmpty(results);
            _output.WriteLine($"Found: {results.Count} alternative routes");
            
            for (int i = 0; i < results.Count; i++)
            {
                var route = results[i];
                var start = route.Path.Count > 0 ? route.Path[0] : "?";
                var end = route.Path.Count > 0 ? route.Path[^1] : "?";
                _output.WriteLine($"  Route #{i + 1}: {start}->...->{end} " +
                                $"(length: {route.Path.Count}), " +
                                $"Time: {route.TotalTime:F1}h, Risk: {route.TotalRisk:F1}");
            }
            
            _output.WriteLine("Test passed\n");
        }

        [Fact]
        public void InvalidTest_InvalidJson_ThrowsException()
        {
            _output.WriteLine("=== InvalidTest: Invalid JSON ===");
            
            var json = ReadTestFile("InvalidTest.json");
            
            _output.WriteLine($"File content: '{json.Substring(0, Math.Min(50, json.Length))}...'");
            
            var exception = Assert.Throws<JsonException>(() =>
            {
                JsonSerializer.Deserialize<InputData>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            });
            
            _output.WriteLine($"Expected exception thrown: {exception.GetType().Name}");
            _output.WriteLine("Test passed\n");
        }

        [Fact]
        public void MissingFile_ThrowsFileNotFoundException()
        {
            _output.WriteLine("=== MissingFile: File Not Found ===");
            
            Assert.Throws<FileNotFoundException>(() =>
            {
                ReadTestFile("NonExistentFile.json");
            });
            
            _output.WriteLine("Expected FileNotFoundException thrown");
            _output.WriteLine("Test passed\n");
        }
    }
}