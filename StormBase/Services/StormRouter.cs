using System;
using System.Collections.Generic;
using System.Linq;
using StormBase.Models;

namespace StormBase.Services
{
    public class StormRouter
    {
        private Dictionary<string, List<Route>> _graph = new Dictionary<string, List<Route>>();
        private Dictionary<string, List<Storm>> _stormsByRoute = new Dictionary<string, List<Storm>>();
        private Dictionary<string, (double slowdown, int risk)> _stormCoefficients = new Dictionary<string, (double, int)>();

        public StormRouter()
        {
            InitializeStormCoefficients();
        }

        private void InitializeStormCoefficients()
        {
       _stormCoefficients = new Dictionary<string, (double, int)>
            {
                ["low"] = (1.2, 1),    
                ["medium"] = (1.5, 2), 
                ["high"] = (2.0, 3)    
            };
        }

        public void LoadData(InputData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            
            BuildGraph(data.Routes);
            BuildStormIndex(data.Storms);
        }

        private void BuildGraph(List<Route> routes)
        {
            _graph = new Dictionary<string, List<Route>>();
            foreach (var route in routes)
            {
                if (!_graph.ContainsKey(route.From))
                    _graph[route.From] = new List<Route>();
                _graph[route.From].Add(route);
            }
        }

        private void BuildStormIndex(List<Storm> storms)
        {
            _stormsByRoute = new Dictionary<string, List<Storm>>();
            foreach (var storm in storms)
            {
                if (!_stormsByRoute.ContainsKey(storm.RouteId))
                    _stormsByRoute[storm.RouteId] = new List<Storm>();
                _stormsByRoute[storm.RouteId].Add(storm);
            }
        }

        public List<RouteState> CalculateOptimalRoutes(string startPoint, string endPoint, DateTime departureTime, int maxAlternatives = 3)
        {
            var results = new List<RouteState>();
            var visited = new Dictionary<string, List<RouteState>>();
            var queue = new PriorityQueue<RouteState, double>();

            var initialState = new RouteState
            {
                Node = startPoint,
                CurrentTime = departureTime,
                TotalRisk = 0,
                Path = new List<string> { startPoint },
                TotalTime = 0,
                TotalTravelTime = 0,
                TotalWaitTime = 0,
                Segments = new List<RouteSegment>()
            };

            queue.Enqueue(initialState, 0);

            while (queue.Count > 0 && results.Count < maxAlternatives)
            {
                var currentState = queue.Dequeue();

                if (currentState.Node == endPoint)
                {
                    results.Add(currentState);
                    continue;
                }

                var nextSteps = GetNextSteps(currentState);
                
                foreach (var nextStep in nextSteps)
                {
                    if (nextStep == null) continue;
                    
                    if (!IsStateWorseThanExisting(visited, nextStep))
                    {
                        AddToVisited(visited, nextStep);
                        double priority = nextStep.TotalTime; 
                        queue.Enqueue(nextStep, priority);
                    }
                }
            }

            return results;
        }

        private List<RouteState?> GetNextSteps(RouteState currentState)
        {
            var nextSteps = new List<RouteState?>();

            if (!_graph.ContainsKey(currentState.Node))
                return nextSteps;

            foreach (var route in _graph[currentState.Node])
            {
                var immediateState = CalculateRouteStep(currentState, route, wait: false);
                if (immediateState != null)
                    nextSteps.Add(immediateState);

                var waitState = CalculateRouteStep(currentState, route, wait: true);
                if (waitState != null)
                    nextSteps.Add(waitState);
            }

            return nextSteps;
        }

        private RouteState? CalculateRouteStep(RouteState currentState, Route route, bool wait)
        {
            string routeId = $"{route.From}-{route.To}";
            DateTime startTime = currentState.CurrentTime;
            double travelTime = route.BaseTime;
            double riskAddition = 0;
            double slowdownCoefficient = 1.0;
            string? stormSeverity = null;

            var activeStorm = GetActiveStorm(routeId, startTime);
            
            RouteSegment? waitSegment = null;
            
            if (activeStorm != null)
            {
                if (wait)
                {
                    DateTime waitUntil = activeStorm.EndTime;
                    if (waitUntil <= startTime)
                        return null;

                    double waitHours = (waitUntil - startTime).TotalHours;
                    
                    waitSegment = new RouteSegment
                    {
                        Type = "Wait",
                        FromNode = currentState.Node,
                        ToNode = currentState.Node,
                        StartTime = startTime,
                        EndTime = waitUntil,
                        Duration = waitHours,
                        Risk = 0,
                        BaseTime = waitHours,
                        ActualTime = waitHours
                    };
                    
                    startTime = waitUntil;
                    
                    var stormAfterWait = GetActiveStorm(routeId, waitUntil);
                    if (stormAfterWait == null)
                    {
                        travelTime = route.BaseTime;
                        riskAddition = 0;
                        slowdownCoefficient = 1.0;
                    }
                    else
                    {
                        var coefficients = _stormCoefficients[stormAfterWait.Severity];
                        travelTime = route.BaseTime * coefficients.slowdown;
                        riskAddition = route.BaseTime * coefficients.risk;
                        slowdownCoefficient = coefficients.slowdown;
                        stormSeverity = stormAfterWait.Severity;
                    }
                }
                else
                {
                    var coefficients = _stormCoefficients[activeStorm.Severity];
                    travelTime = route.BaseTime * coefficients.slowdown;
                    riskAddition = route.BaseTime * coefficients.risk;
                    slowdownCoefficient = coefficients.slowdown;
                    stormSeverity = activeStorm.Severity;
                }
            }
            else if (wait)
            {
                return null;
            }

            DateTime arrivalTime = startTime.AddHours(travelTime);
            
            var travelSegment = new RouteSegment
            {
                Type = "Travel",
                FromNode = currentState.Node,
                ToNode = route.To,
                StartTime = startTime,
                EndTime = arrivalTime,
                Duration = travelTime,
                Risk = riskAddition,
                StormSeverity = stormSeverity,
                BaseTime = route.BaseTime,
                ActualTime = travelTime,
                SlowdownCoefficient = slowdownCoefficient
            };

            var newState = new RouteState
            {
                Node = route.To,
                CurrentTime = arrivalTime,
                TotalRisk = currentState.TotalRisk + riskAddition,
                Path = new List<string>(currentState.Path) { route.To },
                TotalTime = currentState.TotalTime + (arrivalTime - currentState.CurrentTime).TotalHours,
                TotalTravelTime = currentState.TotalTravelTime + travelTime,
                TotalWaitTime = currentState.TotalWaitTime + (waitSegment?.Duration ?? 0),
                Segments = new List<RouteSegment>(currentState.Segments)
            };

            if (waitSegment != null)
            {
                newState.Segments.Add(waitSegment);
            }
            newState.Segments.Add(travelSegment);

            return newState;
        }

        private Storm? GetActiveStorm(string routeId, DateTime time)
        {
            if (_stormsByRoute.ContainsKey(routeId))
            {
                return _stormsByRoute[routeId]
                    .FirstOrDefault(storm => time >= storm.StartTime && time < storm.EndTime);
            }
            return null;
        }

        private bool IsStateWorseThanExisting(Dictionary<string, List<RouteState>> visited, RouteState newState)
        {
            if (!visited.ContainsKey(newState.Node))
                return false;

            var existingStates = visited[newState.Node];
            return existingStates.Any(existing =>
                existing.CurrentTime <= newState.CurrentTime && 
                existing.TotalRisk <= newState.TotalRisk);
        }

        private void AddToVisited(Dictionary<string, List<RouteState>> visited, RouteState state)
        {
            if (!visited.ContainsKey(state.Node))
                visited[state.Node] = new List<RouteState>();
            
            visited[state.Node].Add(state);
        }
    }
}