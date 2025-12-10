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
    public class IntegrationTests
    {
        private readonly ITestOutputHelper _output;
        
        public IntegrationTests(ITestOutputHelper output)
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
        public void EzTestScenario_FindsPath()
        {
            _output.WriteLine("=== EzTest: Simple Scenario ===");
            
            var json = ReadTestFile("EzTest.json");
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
            
            _output.WriteLine($"Found: {results.Count} route(s)");
            _output.WriteLine($"Best route - Time: {bestRoute.TotalTime:F1}h, Risk: {bestRoute.TotalRisk:F1}");
            
            if (bestRoute.Path != null && bestRoute.Path.Count > 0)
            {
                _output.WriteLine($"Path: {string.Join("->", bestRoute.Path)}");
            }
            
            _output.WriteLine("Test passed\n");
        }

        [Fact]
        public void AllDataFiles_AreValidJson()
        {
            _output.WriteLine("=== All Data Files Validation ===");
            
            var dataFiles = new[]
            {
                "EzTest.json",
                "NormTest.json", 
                "HardTest.json",
                "SigmaTest.json",
                "StormRiskTest.json",
                "route_data.json"
            };
            
            foreach (var filename in dataFiles)
            {
                try
                {
                    var json = ReadTestFile(filename);
                    var data = JsonSerializer.Deserialize<InputData>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    _output.WriteLine($"{filename}: Valid JSON, {data.Routes?.Count ?? 0} routes, {data.Storms?.Count ?? 0} storms");
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"X {filename}: ERROR - {ex.Message}");
                    throw;
                }
            }
            
            _output.WriteLine("All files are valid JSON\n");
        }
    }
}