using System.Collections.Concurrent;
using System.Diagnostics;

namespace JobBoardScraper;

public sealed class AdaptiveConcurrencyController
{
    // Текущий желаемый уровень конкуренции (динамический)
    private int _desired;

    private int _lastLoggedConcurrency = -1;
    
    // Границы
    private readonly int _min;
    private readonly int _max;

    // EMA по латентности (мс)
    private double _emaLatencyMs = double.NaN;
    private readonly double _emaAlpha;

    // Порог "быстро" / "медленно"
    private readonly double _fastThresholdMs;
    private readonly double _slowThresholdMs;

    // Период переоценки
    private readonly TimeSpan _evaluationPeriod;

    // Агрессивность изменения
    private readonly int _increaseStep;
    private readonly double _decreaseFactor;

    private readonly object _lock = new();

    public AdaptiveConcurrencyController(
        int defaultConcurrency,
        int minConcurrency = 1,
        int maxConcurrency = 256,
        TimeSpan? fastThreshold = null,     // ответы быстрее этого => можно увеличивать
        TimeSpan? slowThreshold = null,     // ответы медленнее этого => нужно уменьшать
        TimeSpan? evaluationPeriod = null,  // как часто корректируем конкуренцию
        double emaAlpha = 0.2,              // степень сглаживания EMA [0..1]
        int increaseStep = 1,               // на сколько повышать за один шаг
        double decreaseFactor = 0.75,        // во сколько раз снижать (мультипликативно)
        Action<string>? infoLog = null
    )
    {
        if (defaultConcurrency < 1) defaultConcurrency = 1;

        _desired = defaultConcurrency;
        _min = Math.Max(1, minConcurrency);
        _max = Math.Max(_min, maxConcurrency);

        _fastThresholdMs = (fastThreshold ?? TimeSpan.FromMilliseconds(300)).TotalMilliseconds;
        _slowThresholdMs = (slowThreshold ?? TimeSpan.FromMilliseconds(1200)).TotalMilliseconds;

        _evaluationPeriod = evaluationPeriod ?? TimeSpan.FromSeconds(1);
        _emaAlpha = Math.Clamp(emaAlpha, 0.01, 0.99);

        _increaseStep = Math.Max(1, increaseStep);
        _decreaseFactor = Math.Clamp(decreaseFactor, 0.1, 0.99);
        
        Volatile.Write(ref _lastLoggedConcurrency, -1);
    }

    public int DesiredConcurrency => Volatile.Read(ref _desired);
    public double? EmaLatencyMs
    {
        get
        {
            var v = Volatile.Read(ref _emaLatencyMs); // перегрузка для double
            return double.IsNaN(v) ? null : v;
        }
    }

    // Сообщить контроллеру длительность завершившегося HTTP-запроса
    public void ReportLatency(TimeSpan latency)
    {
        var ms = latency.TotalMilliseconds;

        var prev = Volatile.Read(ref _emaLatencyMs);
        double next = double.IsNaN(prev)
            ? ms
            : (1 - _emaAlpha) * prev + _emaAlpha * ms;

        Volatile.Write(ref _emaLatencyMs, next);
    }
    

    // Фоновая подстройка конкуренции
    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(_evaluationPeriod, ct);

                var ema = EmaLatencyMs;
                if (ema is null) continue; // ещё нет данных

                var current = DesiredConcurrency;
                int next = current;

                if (ema < _fastThresholdMs)
                {
                    // Быстро — увеличиваем понемногу (AIMD)
                    next = Math.Min(_max, current + _increaseStep);
                }
                else if (ema > _slowThresholdMs)
                {
                    // Медленно — снижаем более агрессивно
                    next = Math.Max(_min, (int)Math.Ceiling(current * _decreaseFactor));
                }

                if (next != current)
                {
                    Interlocked.Exchange(ref _desired, next);
                    Console.WriteLine($"[Adaptive] EMA={ema:F0} ms, concurrency: {current} -> {next}");
                    Volatile.Write(ref _lastLoggedConcurrency, current);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // нормальная остановка
        }
    }
    
    
}

public static class AdaptiveForEach
{
    // Версия для IEnumerable<T>
    public static async Task ForEachAdaptiveAsync<T>(
        IEnumerable<T> source,
        Func<T, Task> body,
        AdaptiveConcurrencyController controller,
        CancellationToken ct)
    {
        using var enumerator = source.GetEnumerator();
        var running = new HashSet<Task>();

        bool hasMore = true;
        while (true)
        {
            // Добираем до целевой конкуренции
            while (running.Count < controller.DesiredConcurrency && hasMore)
            {
                ct.ThrowIfCancellationRequested();

                hasMore = enumerator.MoveNext();
                if (!hasMore) break;

                var item = enumerator.Current;
                var task = Task.Run(() => body(item), ct);
                running.Add(task);
            }

            if (running.Count == 0 && !hasMore)
                break;

            var finished = await Task.WhenAny(running);
            running.Remove(finished);

            // Пробрасываем исключения тела, чтобы не терять ошибки
            await finished;
        }
    }

    // Версия для IAsyncEnumerable<T>
    public static async Task ForEachAdaptiveAsync<T>(
        IAsyncEnumerable<T> source,
        Func<T, Task> body,
        AdaptiveConcurrencyController controller,
        CancellationToken ct)
    {
        await using var enumerator = source.GetAsyncEnumerator(ct);
        var running = new HashSet<Task>();

        bool hasMore = true;
        while (true)
        {
            // Добираем до целевой конкуренции
            while (running.Count < controller.DesiredConcurrency && hasMore)
            {
                ct.ThrowIfCancellationRequested();

                hasMore = await enumerator.MoveNextAsync();
                if (!hasMore) break;

                var item = enumerator.Current;
                var task = Task.Run(() => body(item), ct);
                running.Add(task);
            }

            if (running.Count == 0 && !hasMore)
                break;

            var finished = await Task.WhenAny(running);
            running.Remove(finished);
            await finished;
        }
    }
}
