using System;
using System.Collections.Generic;
using System.Linq;
using StormBase.Models; 

namespace StormBase.Services.Storms
{
    public class StormProvider : IStormProvider
    {
        private Dictionary<string, List<Storm>> _stormsByRoute = new();
        private readonly Dictionary<string, (double slowdown, int risk)> _stormCoefficients;

        public StormProvider()
        {
            _stormCoefficients = new Dictionary<string, (double, int)>
            {
                ["low"] = (1.1, 1),
                ["medium"] = (1.5, 2),
                ["high"] = (2.0, 3)
            };
        }

        public void LoadStorms(List<Storm> storms)
        {
            _stormsByRoute = storms
                .GroupBy(s => s.RouteId)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        public Storm? GetActiveStorm(string routeId, DateTime time)
        {
            if (_stormsByRoute.ContainsKey(routeId))
                return _stormsByRoute[routeId]
                    .FirstOrDefault(storm => time >= storm.StartTime && time < storm.EndTime);

            return null;
        }

        public (double slowdown, int risk) GetStormCoefficients(string severity)
        {
            return _stormCoefficients[severity];
        }
    }
}
