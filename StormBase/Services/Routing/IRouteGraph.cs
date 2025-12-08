using System.Collections.Generic;
using StormBase.Models;

namespace StormBase.Services.Routing
{
    public interface IRouteGraph
    {
        void BuildGraph(IEnumerable<Route> routes);
        List<Route> GetRoutesFrom(string fromNode);
    }
}
