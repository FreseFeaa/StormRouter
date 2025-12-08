using System.Collections.Generic;
using System.Linq;
using StormBase.Models;

namespace StormBase.Services.Routing
{
    public class RouteGraph : IRouteGraph
    {
        private Dictionary<string, List<Route>> _graph = new Dictionary<string, List<Route>>();

        public void BuildGraph(IEnumerable<Route> routes)
        {
            _graph.Clear();
            foreach (var route in routes)
            {
                if (!_graph.ContainsKey(route.From))
                    _graph[route.From] = new List<Route>();
                _graph[route.From].Add(route);
            }
        }

        public List<Route> GetRoutesFrom(string fromNode)
        {
            return _graph.ContainsKey(fromNode) ? _graph[fromNode] : new List<Route>();
        }
    }
}
