using System;
using System.Collections.Generic;
using Moq;
using StormBase.Models;
using StormBase.Services;
using StormBase.Services.Routing;
using StormBase.Services.Storms;
using Xunit;

namespace StormBase.Tests
{
    public class StormRouterTests
    {
        private readonly Mock<IRouteGraph> _mockRouteGraph;
        private readonly Mock<IStormProvider> _mockStormProvider;
        private readonly StormRouter _router;

        public StormRouterTests()
        {
            _mockRouteGraph = new Mock<IRouteGraph>();
            _mockStormProvider = new Mock<IStormProvider>();
            _router = new StormRouter(_mockRouteGraph.Object, _mockStormProvider.Object);
        }

        [Fact]
        public void Constructor_WithNullArguments_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new StormRouter(null, _mockStormProvider.Object));
            Assert.Throws<ArgumentNullException>(() => new StormRouter(_mockRouteGraph.Object, null));
        }

        [Fact]
        public void LoadData_WithValidData_CallsDependencies()
        {
            var inputData = new InputData
            {
                Routes = new List<Route> { new Route { From = "A", To = "B" } },
                Storms = new List<Storm> { new Storm { RouteId = "A-B" } }
            };

            _router.LoadData(inputData);

            _mockRouteGraph.Verify(g => g.BuildGraph(inputData.Routes), Times.Once);
            _mockStormProvider.Verify(p => p.LoadStorms(inputData.Storms), Times.Once);
        }

        [Fact]
        public void CalculateOptimalRoutes_SimpleRoute_ReturnsCorrectResult()
        {
            var departureTime = new DateTime(2024, 1, 1, 8, 0, 0);
            
            _mockRouteGraph.Setup(g => g.GetRoutesFrom("A")).Returns(new List<Route>
            {
                new Route { From = "A", To = "B", BaseTime = 10 }
            });
            _mockRouteGraph.Setup(g => g.GetRoutesFrom("B")).Returns(new List<Route>());

            _mockStormProvider.Setup(p => p.GetActiveStorm(It.IsAny<string>(), It.IsAny<DateTime>()))
                .Returns((Storm)null);

            var results = _router.CalculateOptimalRoutes("A", "B", departureTime);

            Assert.NotEmpty(results);
            var route = results[0];
            Assert.Equal(new List<string> { "A", "B" }, route.Path);
            Assert.Equal(10, route.TotalTime);
            Assert.Equal(0, route.TotalRisk);
        }

        [Fact]
        public void CalculateOptimalRoutes_WithStorm_AppliesCoefficients()
        {
            var departureTime = new DateTime(2024, 1, 1, 8, 0, 0);
            
            _mockRouteGraph.Setup(g => g.GetRoutesFrom("A")).Returns(new List<Route>
            {
                new Route { From = "A", To = "B", BaseTime = 10 }
            });
            _mockRouteGraph.Setup(g => g.GetRoutesFrom("B")).Returns(new List<Route>());

            var storm = new Storm
            {
                RouteId = "A-B",
                StartTime = departureTime.AddHours(-1),
                EndTime = departureTime.AddHours(100),
                Severity = "high"
            };

            _mockStormProvider.Setup(p => p.GetActiveStorm("A-B", departureTime))
                .Returns(storm);
            
            _mockStormProvider.Setup(p => p.GetStormCoefficients("high"))
                .Returns((2.0, 3));

            var results = _router.CalculateOptimalRoutes("A", "B", departureTime);

            Assert.NotEmpty(results);
            var travelSegment = results[0].Segments.Find(s => s.Type == "Travel");
            
            Assert.Equal(20, travelSegment.ActualTime, 0.1);
            Assert.Equal(2.0, travelSegment.SlowdownCoefficient, 0.1);
            Assert.Equal(30, travelSegment.Risk, 0.1);
        }
    }
}