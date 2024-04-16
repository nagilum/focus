using System.Diagnostics;
using System.Text.Json;
using Focus.Models;
using Focus.Models.Interfaces;
using Microsoft.Playwright;

namespace Focus.Services;

public class CrawlerService(IOptions options) : ICrawlerService
{
    #region Fields
    
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
    /// JSON serializer options.
    /// </summary>
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
    
    /// <summary>
    /// When the crawl started.
    /// </summary>
    private readonly DateTimeOffset _started = DateTimeOffset.Now;
    
    #endregion
    
    #region Properties
    
    /// <summary>
    /// Playwright browser.
    /// </summary>
    private IBrowser? Browser { get; set; }
    
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
    /// <inheritdoc cref="ICrawlerService.DisposePlaywright"/>
    /// </summary>
    public async Task DisposePlaywright()
    {
        if (this.Browser is not null)
        {
            await this.Browser.CloseAsync();
        }
    }
    
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

        await this.WriteQueueToDisk();
    }

    /// <summary>
    /// <inheritdoc cref="ICrawlerService.SetupPlaywright"/>
    /// </summary>
    public async Task<bool> SetupPlaywright()
    {
        try
        {
            Console.WriteLine("Setting up Playwright..");
            
            Microsoft.Playwright.Program.Main(["install"]);
            
            var instance = await Playwright.CreateAsync();

            this.Browser = _options.RenderingEngine switch
            {
                RenderingEngine.Chromium => await instance.Chromium.LaunchAsync(),
                RenderingEngine.Firefox => await instance.Firefox.LaunchAsync(),
                RenderingEngine.Webkit => await instance.Webkit.LaunchAsync(),
                _ => throw new Exception($"Invalid rendering engine: {_options.RenderingEngine}")
            };

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return false;
        }
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
            var page = await this.Browser!.NewPageAsync();
            var watch = Stopwatch.StartNew();
            var res = await page.GotoAsync(entry.Url.ToString())
                      ?? throw new Exception($"Unable to get a valid HTTP response from {entry.Url}");

            watch.Stop();

            if (res.Status is >= 200 and <= 300)
            {
                entry.Finished = DateTimeOffset.Now;
            }
            
            entry.Responses.Add(
                new()
                {
                    StatusCode = res.Status,
                    StatusDescription = res.StatusText,
                    Time = watch.ElapsedMilliseconds
                });
            
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

            responseType = $"{res.Status} {res.StatusText}";

            var isHtml = false;

            foreach (var (key, value) in res.Headers)
            {
                if (!key.Equals("content-type", StringComparison.InvariantCultureIgnoreCase) ||
                    !value.Contains("text/html", StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                isHtml = true;
                break;
            }

            if (isHtml)
            {
                await this.ParseResponseContent(entry, page);
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
    /// Extract new URLs to crawl.
    /// </summary>
    /// <param name="entry">Queue entry.</param>
    /// <param name="page">Playwright page.</param>
    private async Task ParseResponseContent(IQueueEntry entry, IPage page)
    {
        var selectors = new Dictionary<string, string>
        {
            { "a", "href" },
            { "img", "src" },
            { "link", "href" },
            { "script", "src" }
        };

        foreach (var (tag, attr) in selectors)
        {
            var hrefs = page.Locator($"//{tag}[@{attr}]");
            var count = await hrefs.CountAsync();

            for (var i = 0; i < count; i++)
            {
                var url = await hrefs.Nth(i).GetAttributeAsync(attr);

                if (url?.StartsWith('#') is true ||
                    url?.StartsWith('?') is true ||
                    !Uri.TryCreate(entry.Url, url, out var uri) ||
                    !uri.IsAbsoluteUri ||
                    !entry.Url.IsBaseOf(uri) ||
                    string.IsNullOrWhiteSpace(uri.DnsSafeHost))
                {
                    continue;
                }
                
                url = uri.ToString();
                
                var alreadyAdded = _queue.Any(n => n.Url.ToString() == url);

                if (!alreadyAdded)
                {
                    _queue.Add(new QueueEntry(uri));
                }
            }
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
                "Started:  ",
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

            Console.CursorLeft = 6;
            Console.CursorTop = 1;
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write("CTRL+C");
            
            Console.CursorLeft = 10;
            Console.CursorTop = 3;
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.Write($"{_started:yyyy-MM-dd} {_started:HH:mm}");
        }
        
        // Update duration.
        var duration = DateTimeOffset.Now - _started;

        Console.CursorLeft = 10;
        Console.CursorTop = 4;
        Console.ForegroundColor = ConsoleColor.DarkGreen;
        Console.Write(this.GetFormattedTimeSpan(duration));

        // Update progress.
        var finished = _queue.Count(n => n.Finished.HasValue);
        var percent = (int)(100.00 / _queue.Count * finished);
        
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
                var value = _responseTimes[responseTimeRange];
                var count = value.ToString();

                count = new string(' ', 8 - count.Length) + count;
            
                Console.CursorLeft = 0;
                Console.CursorTop = 8 + index;
                Console.ForegroundColor = value > 0 ? ConsoleColor.DarkYellow : ConsoleColor.DarkGray;
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

        int top; 

        lock (_responseTypes)
        {
            top = 13 + _responseTypes.Count;
        }

        Console.CursorLeft = 0;
        Console.CursorTop = top;

        try
        {
            Console.WriteLine($"Writing queue to {path}");
            
            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, _queue, _serializerOptions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    #endregion
}