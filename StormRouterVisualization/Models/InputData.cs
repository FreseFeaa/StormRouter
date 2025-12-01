using System;
using System.Collections.Generic;

namespace StormRouterVisualization.Models
{
    public class InputData
    {
        public string StartPoint { get; set; } = "";
        public string EndPoint { get; set; } = "";
        public DateTime DepartureTime { get; set; }
        public List<Route> Routes { get; set; } = new List<Route>();
        public List<Storm> Storms { get; set; } = new List<Storm>();
    }

    public class Route
    {
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public double Distance { get; set; }
        public double BaseTime { get; set; }
    }

    public class Storm
    {
        public string RouteId { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Severity { get; set; } = "";
    }

    public class RouteSegment
    {
        public string Type { get; set; } = "";
        public string FromNode { get; set; } = "";
        public string ToNode { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double Duration { get; set; }
        public double Risk { get; set; }
        public string? StormSeverity { get; set; }
        public double BaseTime { get; set; }
        public double ActualTime { get; set; }
        public double SlowdownCoefficient { get; set; } = 1.0;
    }

    public class RouteState
    {
        public string Node { get; set; } = "";
        public DateTime CurrentTime { get; set; }
        public double TotalRisk { get; set; }
        public List<string> Path { get; set; } = new List<string>();
        public double TotalTime { get; set; }
        public double TotalTravelTime { get; set; }
        public double TotalWaitTime { get; set; }
        public List<RouteSegment> Segments { get; set; } = new List<RouteSegment>();
    }
}