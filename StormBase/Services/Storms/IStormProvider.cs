public interface IStormProvider
{
    Storm? GetActiveStorm(string routeId, DateTime time);
    (double slowdown, int risk) GetStormCoefficients(string severity);
    void LoadStorms(List<Storm> storms);
}
