using System;
using System.Collections.Generic;
using StormBase.Models;
using StormBase.Services.Storms;
using Xunit;

namespace StormBase.Tests
{
    public class StormProviderTests
    {
        [Fact]
        public void GetActiveStorm_ReturnsCorrectStorm()
        {
            var stormProvider = new StormProvider();
            var storm = new Storm
            {
                RouteId = "A-B",
                StartTime = new DateTime(2024, 1, 1, 10, 0, 0),
                EndTime = new DateTime(2024, 1, 1, 15, 0, 0),
                Severity = "high"
            };

            stormProvider.LoadStorms(new List<Storm> { storm });

            var active = stormProvider.GetActiveStorm("A-B", new DateTime(2024, 1, 1, 12, 0, 0));
            Assert.NotNull(active);
            Assert.Equal("high", active.Severity);

            var inactive = stormProvider.GetActiveStorm("A-B", new DateTime(2024, 1, 1, 16, 0, 0));
            Assert.Null(inactive);
        }

        [Theory]
        [InlineData("low", 1.1, 1)]
        [InlineData("medium", 1.5, 2)]
        [InlineData("high", 2.0, 3)]
        public void GetStormCoefficients_ReturnsCorrectValues(string severity, double expectedSlowdown, int expectedRisk)
        {
            var stormProvider = new StormProvider();

            var (slowdown, risk) = stormProvider.GetStormCoefficients(severity);

            Assert.Equal(expectedSlowdown, slowdown);
            Assert.Equal(expectedRisk, risk);
        }
    }
}