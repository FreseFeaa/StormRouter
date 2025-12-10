using System.Collections.Generic;
using StormBase.Models;
using StormBase.Services.Routing;
using Xunit;

namespace StormBase.Tests
{
    public class RouteGraphTests
    {
        [Fact]
        public void BuildGraph_CreatesCorrectStructure()
        {
            var routeGraph = new RouteGraph();
            var routes = new List<Route>
            {
                new Route { From = "A", To = "B", BaseTime = 10 },
                new Route { From = "A", To = "C", BaseTime = 20 },
                new Route { From = "B", To = "D", BaseTime = 15 }
            };

            routeGraph.BuildGraph(routes);
            
            var fromA = routeGraph.GetRoutesFrom("A");
            var fromB = routeGraph.GetRoutesFrom("B");
            var fromC = routeGraph.GetRoutesFrom("C");

            Assert.Equal(2, fromA.Count);
            Assert.Single(fromB);
            Assert.Empty(fromC);
        }

        [Fact]
        public void GetRoutesFrom_NonExistentNode_ReturnsEmptyList()
        {
            var routeGraph = new RouteGraph();
            routeGraph.BuildGraph(new List<Route>());

            var result = routeGraph.GetRoutesFrom("NonExistent");

            Assert.NotNull(result);
            Assert.Empty(result);
        }
    }
}