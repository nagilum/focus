using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Focus;

public class CrawlerService(IOptions options) : ICrawlerService
{
    #region Fields

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
    
    /// <summary>
    /// Parsed options.
    /// </summary>
    private readonly IOptions _options = options;

    /// <summary>
    /// Queue entries.
    /// </summary>
    private readonly List<QueueEntry> _queue = [];

    /// <summary>
    /// Response time ranges.
    /// </summary>
    private readonly Dictionary<ResponseTimeRange, int> _responseTimes = new()
    {
        { ResponseTimeRange.LessThan450Ms, 0 },
        { ResponseTimeRange.MoreThan450MsLessThan900Ms, 0 },
        { ResponseTimeRange.MoreThan900Ms, 0 }
    };

    /// <summary>
    /// Response types.
    /// </summary>
    private readonly Dictionary<string, int> _responseTypes = new();
    
    /// <summary>
    /// When the crawl started.
    /// </summary>
    private readonly DateTimeOffset _started = DateTimeOffset.Now;
    
    #endregion
    
    #region Properties
    
    /// <summary>
    /// Response types on last loop.
    /// </summary>
    private int ResponseTypes { get; set; }

    /// <summary>
    /// Window height.
    /// </summary>
    private int WindowHeight { get; set; } = Console.WindowHeight;

    /// <summary>
    /// Window width.
    /// </summary>
    private int WindowWidth { get; set; } = Console.WindowWidth;
    
    #endregion
    
    #region ICrawlerService implementations
    
    /// <summary>
    /// <inheritdoc cref="ICrawlerService.Run"/>
    /// </summary>
    public async Task Run(CancellationToken cancellationToken)
    {
        _queue.AddRange(_options.Urls.Select(n => new QueueEntry(n)));

        this.UpdateUi(true);

        while (!cancellationToken.IsCancellationRequested)
        {
            var entries = _queue
                .Where(n => !n.Finished.HasValue)
                .ToList();

            if (entries.Count == 0)
            {
                break;
            }

            await Parallel.ForEachAsync(entries, cancellationToken, async (entry, token) =>
                await this.HandleQueueEntry(entry, token));
            
            this.UpdateUi(false);
        }
        
        Console.ResetColor();
        Console.Clear();

        await this.WriteQueueToDisk();
    }

    #endregion

    #region Helper functions
    
    /// <summary>
    /// Get a more human-readable format of a TimeSpan value.
    /// </summary>
    /// <param name="ts">Time span to format.</param>
    /// <returns>Formatted duration.</returns>
    private string GetFormattedTimeSpan(TimeSpan ts)
    {
        var output = string.Empty;

        if (ts.Hours > 1)
        {
            output += $"{ts.Hours}h ";
        }

        if (ts.Minutes > 0)
        {
            output += $"{ts.Minutes}m ";
        }

        if (ts.Seconds > 0)
        {
            output += $"{ts.Seconds}s";
        }
        else
        {
            output += "0s";
        }

        output += "              ";

        return output;
    }
    
    /// <summary>
    /// Crawl the given queue entry and update tracking data.
    /// </summary>
    /// <param name="entry">Queue entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task HandleQueueEntry(QueueEntry entry, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        
        entry.Attempts++;

        string responseType;
        
        try
        {
            using var client = new HttpClient();
            
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(Program.Name, Program.Version));
            
            client.Timeout = _options.RequestTimeout;

            var watch = Stopwatch.StartNew();
            var res = await client.GetAsync(entry.Url, cancellationToken);

            if (res.IsSuccessStatusCode)
            {
                entry.Finished = DateTimeOffset.Now;
            }
            
            watch.Stop();
            
            entry.Responses.Add(new Response(res, watch.ElapsedMilliseconds));

            var responseTimeRange = watch.ElapsedMilliseconds switch
            {
                < 450 => ResponseTimeRange.LessThan450Ms,
                > 900 => ResponseTimeRange.MoreThan900Ms,
                _ => ResponseTimeRange.MoreThan450MsLessThan900Ms
            };

            lock (_responseTimes)
            {
                _responseTimes[responseTimeRange]++;
            }

            responseType = $"{(int)res.StatusCode} {Tools.GetStatusCodeDescription((int)res.StatusCode)}";
            
            if (res.Content.Headers.ContentType?.MediaType?.Contains("text/html", StringComparison.InvariantCultureIgnoreCase) is true)
            {
                await this.ParseResponseContent(entry, res.Content, cancellationToken);
            }
        }
        catch (TimeoutException)
        {
            responseType = "Request Timeout";
            entry.Errors.Add(new RequestError($"Request timeout after {_options.RequestTimeout.TotalSeconds} second(s)."));
        }
        catch (Exception ex)
        {
            responseType = ex.Message;
            entry.Errors.Add(new RequestError(ex.Message));
        }

        if (!entry.Finished.HasValue &&
            entry.Attempts > _options.MaxRetryAttempts)
        {
            entry.Finished = DateTimeOffset.Now;
        }

        lock (_responseTypes)
        {
            if (_responseTypes.TryGetValue(responseType, out var value))
            {
                _responseTypes[responseType] = ++value;
            }
            else
            {
                _responseTypes.Add(responseType, 1);
            }
        }
    }

    /// <summary>
    /// Attempt to parse the response content for new URLs to add to the queue.
    /// </summary>
    /// <param name="entry">Queue entry.</param>
    /// <param name="content">HTTP response content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task ParseResponseContent(QueueEntry entry, HttpContent content, CancellationToken cancellationToken)
    {
        try
        {
            var html = await content.ReadAsStringAsync(cancellationToken);
            
            // TODO: Analyze HTML and add new URLs to queue.
        }
        catch
        {
            // Do nothing.
        }
    }

    /// <summary>
    /// Update/redraw the UI.
    /// </summary>
    /// <param name="redraw">Whether to redraw the whole UI.</param>
    private void UpdateUi(bool redraw)
    {
        if (!redraw)
        {
            if (this.WindowHeight != Console.WindowHeight ||
                this.WindowWidth != Console.WindowWidth)
            {
                this.WindowHeight = Console.WindowHeight;
                this.WindowWidth = Console.WindowWidth;
                
                redraw = true;
            }

            lock (_responseTypes)
            {
                if (this.ResponseTypes != _responseTypes.Count)
                {
                    this.ResponseTypes = _responseTypes.Count;
                    redraw = true;
                }
            }
        }

        if (redraw)
        {
            var lines = new List<string>
            {
                Program.NameAndVersion,
                "Press CTRL+C to abort",
                "",
                $"Started:  {_started:yyyy-MM-dd} {_started:HH:mm}",
                "Duration: ",
                "Progress: ",
                "Requests: ",
                "",
                "          < 450 ms",
                "          > 450 ms < 900 ms",
                "          > 900 ms",
                ""
            };
            
            lock (_responseTypes)
            {
                lines.AddRange(_responseTypes.Select(n => $"          {n.Key}"));
            }

            Console.CursorVisible = false;
            Console.ResetColor();
            Console.Clear();

            foreach (var line in lines)
            {
                Console.WriteLine(line);
            }
        }
        
        // Update duration.
        var duration = DateTimeOffset.Now - _started;

        Console.CursorLeft = 10;
        Console.CursorTop = 4;
        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.Write(this.GetFormattedTimeSpan(duration));

        // Update progress.
        var finished = _queue.Count(n => n.Finished.HasValue);
        var percent = 100.00 / _queue.Count * finished;
        
        Console.CursorLeft = 10;
        Console.CursorTop = 5;
        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.Write($"{finished} ({percent}%) of {_queue.Count}");

        // Update requests per. second.
        var requestsPerSecond = finished > 0
            ? duration.TotalSeconds / finished
            : 0;
        
        Console.CursorLeft = 10;
        Console.CursorTop = 6;
        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.Write($"~{(int)requestsPerSecond}/s");

        // Update response times.
        lock (_responseTimes)
        {
            foreach (var responseTimeRange in Enum.GetValues<ResponseTimeRange>())
            {
                var index = (int)responseTimeRange;
                var count = _responseTimes[responseTimeRange].ToString();

                count = new string(' ', 8 - count.Length) + count;
            
                Console.CursorLeft = 0;
                Console.CursorTop = 8 + index;
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write(count);
            }
        }

        // Update response types.
        var top = 11;

        lock (_responseTypes)
        {
            foreach (var responseType in _responseTypes.OrderBy(n => n.Key))
            {
                var count = responseType.Value.ToString();
            
                count = new string(' ', 8 - count.Length) + count;
            
                Console.CursorLeft = 0;
                Console.CursorTop = ++top;
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.Write(count);
            }            
        }
    }

    /// <summary>
    /// Write queue to disk.
    /// </summary>
    private async Task WriteQueueToDisk()
    {
        var path = Path.Combine(
            Directory.GetCurrentDirectory(),
            $"queue-{_started:yyyy-MM-dd-HH-mm-ss}.json");

        try
        {
            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, _queue, _serializerOptions);
            
            Console.WriteLine($"Wrote queue to {path}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    #endregion
}