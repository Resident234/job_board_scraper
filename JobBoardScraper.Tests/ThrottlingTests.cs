using Xunit;
using JobBoardScraper.Infrastructure.Throttling;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace JobBoardScraper.Tests
{
    public class LinearThrottleTests
    {
        [Fact]
        public void Constructor_ShouldClampValues()
        {
            var throttle = new LinearThrottle(0, -100, -50, -200);
            
            Assert.Equal(1, throttle.MaxAttempts);
            // baseDelayMs=0, stepDelayMs=0, maxDelayMs=0 (since maxDelayMs must be >= baseDelayMs)
            Assert.Equal(0, throttle.CurrentDelayMs);
        }

        [Fact]
        public void RegisterFailure_ShouldIncrementAttempts()
        {
            var throttle = new LinearThrottle(3);
            
            Assert.Equal(0, throttle.FailedAttempts);
            Assert.Equal(1, throttle.CurrentAttempt);
            Assert.True(throttle.CanAttempt);

            throttle.RegisterFailure();
            Assert.Equal(1, throttle.FailedAttempts);
            Assert.Equal(2, throttle.CurrentAttempt);

            throttle.RegisterFailure();
            throttle.RegisterFailure();
            
            Assert.Equal(3, throttle.FailedAttempts);
            Assert.False(throttle.CanAttempt);
            Assert.True(throttle.IsExhausted);
        }

        [Fact]
        public void CurrentDelay_FixedDelay_ShouldBeConstant()
        {
            var throttle = new LinearThrottle(5, baseDelayMs: 1000, stepDelayMs: 0);
            
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal(1000, throttle.CurrentDelayMs);
                throttle.RegisterFailure();
            }
        }

        [Fact]
        public void CurrentDelay_LinearIncrease_ShouldIncrease()
        {
            var throttle = new LinearThrottle(5, baseDelayMs: 1000, stepDelayMs: 500);
            
            // Attempt 1 (Failed 0): 1000 + 0*500 = 1000
            Assert.Equal(1000, throttle.CurrentDelayMs);
            
            throttle.RegisterFailure();
            // Attempt 2 (Failed 1): 1000 + 1*500 = 1500
            Assert.Equal(1500, throttle.CurrentDelayMs);
            
            throttle.RegisterFailure();
            // Attempt 3 (Failed 2): 1000 + 2*500 = 2000
            Assert.Equal(2000, throttle.CurrentDelayMs);
        }

        [Fact]
        public void CurrentDelay_ShouldBeCappedByMaxDelay()
        {
            var throttle = new LinearThrottle(10, baseDelayMs: 1000, stepDelayMs: 1000, maxDelayMs: 2500);
            
            throttle.RegisterFailure(); // 1000
            throttle.RegisterFailure(); // 2000
            throttle.RegisterFailure(); // 3000 -> 2500
            
            Assert.Equal(2500, throttle.CurrentDelayMs);
        }

        [Fact]
        public void CalculateDelay_StaticMethod_ShouldWorkCorrectly()
        {
            Assert.Equal(1000, LinearThrottle.CalculateDelay(1, 1000, 0, 5000));
            Assert.Equal(1500, LinearThrottle.CalculateDelay(2, 1000, 500, 5000));
            Assert.Equal(5000, LinearThrottle.CalculateDelay(10, 1000, 1000, 5000));
        }

        [Theory]
        [InlineData(500, "500мс")]
        [InlineData(1000, "1,0с")]
        [InlineData(1500, "1,5с")]
        [InlineData(10500, "10,5с")]
        public void GetDelayDescription_ShouldFormatCorrectly(int ms, string expected)
        {
            Assert.Equal(expected, LinearThrottle.GetDelayDescription(ms));
        }
    }

    public class ExponentialBackoffTests
    {
        [Fact]
        public void CalculateDelay_ShouldStayWithinJitterRange()
        {
            int baseDelay = 1000;
            int maxDelay = 30000;
            double jitterFactor = 0.2; // ±20%
            int attempt = 1;

            // For attempt 1: delay = 1000. Range: [800, 1200]
            for (int i = 0; i < 100; i++)
            {
                int delay = ExponentialBackoff.CalculateDelay(attempt, baseDelay, maxDelay, jitterFactor);
                Assert.InRange(delay, 800, 1200);
            }
        }

        [Fact]
        public void CalculateDelay_ShouldIncreaseExponentially()
        {
            // Test without jitter to verify growth
            int baseDelay = 1000;
            int maxDelay = 100000;
            
            // With jitterFactor = 0, it should be exact
            int delay1 = ExponentialBackoff.CalculateDelay(1, baseDelay, maxDelay, 0);
            int delay2 = ExponentialBackoff.CalculateDelay(2, baseDelay, maxDelay, 0);
            int delay3 = ExponentialBackoff.CalculateDelay(3, baseDelay, maxDelay, 0);

            Assert.Equal(1000, delay1);
            Assert.Equal(2000, delay2);
            Assert.Equal(4000, delay3);
        }

        [Fact]
        public void CalculateDelay_ShouldCapAtMaxDelay()
        {
            int baseDelay = 1000;
            int maxDelay = 5000;
            
            // Attempt 10 would be 1000 * 2^9 = 512,000
            // With 0 jitter, should be exactly maxDelay
            int delay = ExponentialBackoff.CalculateDelay(10, baseDelay, maxDelay, 0);
            Assert.Equal(maxDelay, delay);
        }

        [Fact]
        public void CalculateDelay_ShouldHaveMinimum100Ms()
        {
            // Very small base delay and high jitter that could push it negative
            int delay = ExponentialBackoff.CalculateDelay(1, 10, 1000, 1.0);
            Assert.True(delay >= 100);
        }

        [Fact]
        public void ServerErrorDelay_ShouldUseSpecificParams()
        {
            // Base 2000, Attempt 1, Jitter 0 -> 2000
            int delay = ExponentialBackoff.CalculateServerErrorDelay(1);
            // We can't use 0 jitter here since it's hardcoded, so check range ±30% of 2000: [1400, 2600]
            Assert.InRange(delay, 1400, 2600);
        }

        [Fact]
        public void ProxyErrorDelay_ShouldUseSpecificParams()
        {
            // Base 500, Attempt 1, Jitter 0.2 -> [400, 600]
            int delay = ExponentialBackoff.CalculateProxyErrorDelay(1);
            Assert.InRange(delay, 400, 600);
        }

        [Theory]
        [InlineData(500, "500мс")]
        [InlineData(1200, "1,2с")]
        [InlineData(10000, "10,0с")]
        public void GetDelayDescription_ShouldFormatCorrectly(int ms, string expected)
        {
            Assert.Equal(expected, ExponentialBackoff.GetDelayDescription(ms));
        }
    }
}