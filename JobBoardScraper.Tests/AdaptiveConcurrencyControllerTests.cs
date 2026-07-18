using Xunit;
using JobBoardScraper.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace JobBoardScraper.Tests
{
    public class AdaptiveConcurrencyControllerTests
    {
        #region Конструктор

        [Fact]
        public void Constructor_ShouldClampDefaultConcurrencyToAtLeastOne()
        {
            var ctrl = new AdaptiveConcurrencyController(0);
            Assert.Equal(1, ctrl.DesiredConcurrency);
        }

        [Fact]
        public void Constructor_ShouldRespectMinConcurrency()
        {
            var ctrl = new AdaptiveConcurrencyController(5, minConcurrency: 2);
            // При конструировании _desired = defaultConcurrency, если >= min
            Assert.Equal(5, ctrl.DesiredConcurrency);
        }

        [Fact]
        public void Constructor_ShouldClampMinToAtLeastOne()
        {
            var ctrl = new AdaptiveConcurrencyController(10, minConcurrency: -5);
            Assert.Equal(10, ctrl.DesiredConcurrency); // _desired не меняется, но _min = 1
        }

        [Fact]
        public void Constructor_MaxShouldNotBeLessThanMin()
        {
            var ctrl = new AdaptiveConcurrencyController(5, minConcurrency: 10, maxConcurrency: 3);
            // _max = Math.Max(_min, maxConcurrency) => _max = 10
            Assert.Equal(5, ctrl.DesiredConcurrency); // _desired внутри [1, max]
        }

        [Fact]
        public void Constructor_ShouldSetDefaultValuesCorrectly()
        {
            var ctrl = new AdaptiveConcurrencyController(10);
            Assert.Equal(10, ctrl.DesiredConcurrency);
            Assert.Null(ctrl.EmaLatencyMs); // нет данных
        }

        #endregion

        #region ReportLatency

        [Fact]
        public void ReportLatency_ShouldInitializeEmaOnFirstCall()
        {
            var ctrl = new AdaptiveConcurrencyController(10);

            ctrl.ReportLatency(TimeSpan.FromMilliseconds(500));

            Assert.NotNull(ctrl.EmaLatencyMs);
            Assert.Equal(500, ctrl.EmaLatencyMs!.Value, 1);
        }

        [Fact]
        public void ReportLatency_ShouldUpdateEmaOnSubsequentCalls()
        {
            var ctrl = new AdaptiveConcurrencyController(10);
            ctrl.ReportLatency(TimeSpan.FromMilliseconds(100));
            ctrl.ReportLatency(TimeSpan.FromMilliseconds(200));

            // EMA = (1 - alpha) * prev + alpha * new
            // alpha = 0.2
            // prev = 100
            // EMA = 0.8*100 + 0.2*200 = 80 + 40 = 120
            Assert.NotNull(ctrl.EmaLatencyMs);
            Assert.Equal(120, ctrl.EmaLatencyMs!.Value, 1);
        }

        [Fact]
        public void ReportLatency_ShouldWorkWithZeroLatency()
        {
            var ctrl = new AdaptiveConcurrencyController(10);
            ctrl.ReportLatency(TimeSpan.Zero);

            Assert.NotNull(ctrl.EmaLatencyMs);
            Assert.Equal(0, ctrl.EmaLatencyMs!.Value, 1);
        }

        [Fact]
        public void ReportLatency_ShouldWorkWithLargeValues()
        {
            var ctrl = new AdaptiveConcurrencyController(10);
            ctrl.ReportLatency(TimeSpan.FromDays(1));
            ctrl.ReportLatency(TimeSpan.FromDays(1));

            Assert.NotNull(ctrl.EmaLatencyMs);
            Assert.True(ctrl.EmaLatencyMs > 0);
        }

        #endregion

        #region AIMD – адаптивное изменение конкуренции

        [Fact]
        public void AfterFastLatency_DesiredShouldIncrease()
        {
            // fastThreshold по умолчанию = 300ms
            // Если EMA < 300ms, то увеличиваем на increaseStep (по умолчанию 1)
            var ctrl = new AdaptiveConcurrencyController(
                defaultConcurrency: 5,
                maxConcurrency: 100,
                evaluationPeriod: TimeSpan.FromMilliseconds(10));

            // Симулируем быстрые ответы
            for (int i = 0; i < 10; i++)
            {
                ctrl.ReportLatency(TimeSpan.FromMilliseconds(100));
            }

            // Запускаем RunAsync на короткое время
            using var cts = new CancellationTokenSource(200);
            ctrl.RunAsync(cts.Token).GetAwaiter().GetResult();

            // Desired должен увеличиться
            Assert.True(ctrl.DesiredConcurrency > 5,
                $"Expected >5, actual = {ctrl.DesiredConcurrency}");
        }

        [Fact]
        public void AfterSlowLatency_DesiredShouldDecrease()
        {
            // slowThreshold по умолчанию = 1200ms
            // Если EMA > 1200ms, то снижаем на decreaseFactor (по умолчанию 0.75)
            var ctrl = new AdaptiveConcurrencyController(
                defaultConcurrency: 10,
                minConcurrency: 1,
                evaluationPeriod: TimeSpan.FromMilliseconds(10));

            // Симулируем медленные ответы
            for (int i = 0; i < 10; i++)
            {
                ctrl.ReportLatency(TimeSpan.FromMilliseconds(3000));
            }

            using var cts = new CancellationTokenSource(200);
            ctrl.RunAsync(cts.Token).GetAwaiter().GetResult();

            Assert.True(ctrl.DesiredConcurrency < 10,
                $"Expected <10, actual = {ctrl.DesiredConcurrency}");
        }

        [Fact]
        public void DesiredConcurrency_ShouldNotDropBelowMin()
        {
            var ctrl = new AdaptiveConcurrencyController(
                defaultConcurrency: 2,
                minConcurrency: 1,
                maxConcurrency: 100,
                slowThreshold: TimeSpan.FromMilliseconds(1), // все ответы медленные
                decreaseFactor: 0.5,
                evaluationPeriod: TimeSpan.FromMilliseconds(10));

            for (int i = 0; i < 5; i++)
            {
                ctrl.ReportLatency(TimeSpan.FromSeconds(10));
            }

            using var cts = new CancellationTokenSource(200);
            ctrl.RunAsync(cts.Token).GetAwaiter().GetResult();

            Assert.True(ctrl.DesiredConcurrency >= 1);
        }

        [Fact]
        public void DesiredConcurrency_ShouldNotExceedMax()
        {
            var ctrl = new AdaptiveConcurrencyController(
                defaultConcurrency: 5,
                minConcurrency: 1,
                maxConcurrency: 10,
                fastThreshold: TimeSpan.FromMilliseconds(10000), // все ответы быстрые
                increaseStep: 5,
                evaluationPeriod: TimeSpan.FromMilliseconds(10));

            for (int i = 0; i < 10; i++)
            {
                ctrl.ReportLatency(TimeSpan.FromMilliseconds(1));
            }

            using var cts = new CancellationTokenSource(200);
            ctrl.RunAsync(cts.Token).GetAwaiter().GetResult();

            Assert.True(ctrl.DesiredConcurrency <= 10);
        }

        [Fact]
        public void DesiredConcurrency_ShouldNotChange_WhenLatencyIsBetweenThresholds()
        {
            var ctrl = new AdaptiveConcurrencyController(
                defaultConcurrency: 5,
                fastThreshold: TimeSpan.FromMilliseconds(100),
                slowThreshold: TimeSpan.FromMilliseconds(500),
                evaluationPeriod: TimeSpan.FromMilliseconds(10));

            // EMA = 300ms — между 100 и 500
            for (int i = 0; i < 5; i++)
            {
                ctrl.ReportLatency(TimeSpan.FromMilliseconds(300));
            }

            using var cts = new CancellationTokenSource(200);
            ctrl.RunAsync(cts.Token).GetAwaiter().GetResult();

            Assert.Equal(5, ctrl.DesiredConcurrency);
        }

        #endregion

        #region RunAsync – базовая жизнеспособность

        [Fact]
        public async Task RunAsync_ShouldStopOnCancellation()
        {
            var ctrl = new AdaptiveConcurrencyController(5);
            using var cts = new CancellationTokenSource(100);

            // Не должно выбросить исключение
            await ctrl.RunAsync(cts.Token);

            Assert.True(true); // дожили до отмены без ошибки
        }

        [Fact]
        public void RunAsync_ShouldNotThrowWithNoLatencyData()
        {
            // Если нет вызовов ReportLatency, EMA = null, и RunAsync ничего не делает
            var ctrl = new AdaptiveConcurrencyController(5);
            using var cts = new CancellationTokenSource(100);

            // Не должно быть исключения
            ctrl.RunAsync(cts.Token).GetAwaiter().GetResult();
        }

        #endregion

        #region Dispose

        [Fact]
        public void Dispose_ShouldNotThrow()
        {
            var ctrl = new AdaptiveConcurrencyController(5);
            ctrl.Dispose();
            // Не должно быть исключения
        }

        #endregion
    }

    public class AdaptiveForEachTests
    {
        [Fact]
        public async Task ForEachAdaptiveAsync_ShouldProcessAllItems()
        {
            var items = Enumerable.Range(0, 10).ToList();
            var processed = new List<int>();

            var controller = new AdaptiveConcurrencyController(
                defaultConcurrency: 4,
                evaluationPeriod: TimeSpan.FromMilliseconds(50));

            await AdaptiveForEach.ForEachAdaptiveAsync(
                items,
                async item =>
                {
                    await Task.Delay(10);
                    lock (processed)
                    {
                        processed.Add(item);
                    }
                },
                controller,
                CancellationToken.None);

            Assert.Equal(10, processed.Count);
            Assert.Equal(items.OrderBy(x => x), processed.OrderBy(x => x));
        }

        [Fact]
        public async Task ForEachAdaptiveAsync_ShouldPropagateException()
        {
            var items = new[] { 1, 2, 3 };
            var controller = new AdaptiveConcurrencyController(2);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                AdaptiveForEach.ForEachAdaptiveAsync(
                    items,
                    item => throw new InvalidOperationException("Test error"),
                    controller,
                    CancellationToken.None));

            Assert.Contains("Test error", ex.Message);
        }

        [Fact]
        public async Task ForEachAdaptiveAsync_ShouldRespectCancellationToken()
        {
            var items = Enumerable.Range(0, 100);
            var controller = new AdaptiveConcurrencyController(10);
            using var cts = new CancellationTokenSource(50);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                AdaptiveForEach.ForEachAdaptiveAsync(
                    items,
                    async item =>
                    {
                        await Task.Delay(500);
                    },
                    controller,
                    cts.Token));
        }

        [Fact]
        public async Task ForEachAdaptiveAsync_ShouldWorkWithEmptySource()
        {
            var items = Array.Empty<int>();
            var controller = new AdaptiveConcurrencyController(4);

            await AdaptiveForEach.ForEachAdaptiveAsync(
                items,
                _ => Task.CompletedTask,
                controller,
                CancellationToken.None);

            Assert.True(true); // Не должно быть исключения
        }

        [Fact]
        public async Task ForEachAdaptiveAsync_ShouldWorkWithSingleItem()
        {
            var items = new[] { 42 };
            var processed = false;

            var controller = new AdaptiveConcurrencyController(1);
            await AdaptiveForEach.ForEachAdaptiveAsync(
                items,
                item =>
                {
                    processed = true;
                    return Task.CompletedTask;
                },
                controller,
                CancellationToken.None);

            Assert.True(processed);
        }

        [Fact]
        public async Task ForEachAdaptiveAsync_ShouldAccountDesiredConcurrency()
        {
            var items = Enumerable.Range(0, 20).ToList();
            var maxParallel = 0;
            var currentParallel = 0;

            var controller = new AdaptiveConcurrencyController(
                defaultConcurrency: 5,
                evaluationPeriod: TimeSpan.FromMilliseconds(50));

            await AdaptiveForEach.ForEachAdaptiveAsync(
                items,
                async item =>
                {
                    Interlocked.Increment(ref currentParallel);
                    InterlockedExchangeMax(ref maxParallel, currentParallel);
                    await Task.Delay(30);
                    Interlocked.Decrement(ref currentParallel);
                },
                controller,
                CancellationToken.None);

            // Должны были запуститься параллельно хотя бы несколько
            Assert.True(maxParallel >= 2,
                $"Expected maxParallel >= 2, actual = {maxParallel}");
        }

        private static void InterlockedExchangeMax(ref int target, int value)
        {
            int initial;
            do
            {
                initial = target;
                if (value <= initial) break;
            }
            while (Interlocked.CompareExchange(ref target, value, initial) != initial);
        }
    }
}