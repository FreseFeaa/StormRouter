using System;
using System.Collections.Generic;
using System.Linq;
using StormBase.Models;
using StormBase.Services.Routing;
using StormBase.Services.Storms;

namespace StormBase.Services
{
    public class StormRouter
    {
        private readonly IRouteGraph _routeGraph;
        private readonly IStormProvider _stormProvider;

        public StormRouter(IRouteGraph routeGraph, IStormProvider stormProvider)
        {
            _routeGraph = routeGraph ?? throw new ArgumentNullException(nameof(routeGraph));
            _stormProvider = stormProvider ?? throw new ArgumentNullException(nameof(stormProvider));
        }

        public void LoadData(InputData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            _routeGraph.BuildGraph(data.Routes);
            _stormProvider.LoadStorms(data.Storms);
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

                foreach (var nextState in GetNextSteps(currentState))
                {
                    if (nextState == null) continue;

                    if (!IsStateWorseThanExisting(visited, nextState))
                    {
                        AddToVisited(visited, nextState);
                        queue.Enqueue(nextState, nextState.TotalTime);
                    }
                }
            }

            return results;
        }

        private List<RouteState?> GetNextSteps(RouteState currentState)
        {
            var nextSteps = new List<RouteState?>();

            foreach (var route in _routeGraph.GetRoutesFrom(currentState.Node))
            {
                var immediateState = CalculateRouteStep(currentState, route, wait: false);
                if (immediateState != null) nextSteps.Add(immediateState);

                var waitState = CalculateRouteStep(currentState, route, wait: true);
                if (waitState != null) nextSteps.Add(waitState);
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

            var activeStorm = _stormProvider.GetActiveStorm(routeId, startTime);
            RouteSegment? waitSegment = null;

            if (activeStorm != null)
            {
                if (wait)
                {
                    DateTime waitUntil = activeStorm.EndTime;
                    if (waitUntil <= startTime) return null;

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

                    var stormAfterWait = _stormProvider.GetActiveStorm(routeId, waitUntil);
                    if (stormAfterWait != null)
                    {
                        var coeff = _stormProvider.GetStormCoefficients(stormAfterWait.Severity);
                        travelTime = route.BaseTime * coeff.slowdown;
                        riskAddition = route.BaseTime * coeff.risk;
                        slowdownCoefficient = coeff.slowdown;
                        stormSeverity = stormAfterWait.Severity;
                    }
                }
                else
                {
                    var coeff = _stormProvider.GetStormCoefficients(activeStorm.Severity);
                    travelTime = route.BaseTime * coeff.slowdown;
                    riskAddition = route.BaseTime * coeff.risk;
                    slowdownCoefficient = coeff.slowdown;
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

            if (waitSegment != null) newState.Segments.Add(waitSegment);
            newState.Segments.Add(travelSegment);

            return newState;
        }

        private bool IsStateWorseThanExisting(Dictionary<string, List<RouteState>> visited, RouteState newState)
        {
            if (!visited.ContainsKey(newState.Node)) return false;

            return visited[newState.Node].Any(existing =>
                existing.CurrentTime <= newState.CurrentTime &&
                existing.TotalRisk <= newState.TotalRisk);
        }

        private void AddToVisited(Dictionary<string, List<RouteState>> visited, RouteState state)
        {
            if (!visited.ContainsKey(state.Node)) visited[state.Node] = new List<RouteState>();
            visited[state.Node].Add(state);
        }
    }
}
