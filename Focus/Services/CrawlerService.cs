using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Focus.Models;
using Focus.Models.Interfaces;
using Microsoft.Playwright;

namespace Focus.Services;

public class CrawlerService(IOptions options) : ICrawlerService
{
    #region Fields

    /// <summary>
    /// Number of active requests.
    /// </summary>
    private int _activeRequests;
    
    /// <summary>
    /// Parsed options.
    /// </summary>
    private readonly IOptions _options = options;

    /// <summary>
    /// Queue entries.
    /// </summary>
    private readonly ConcurrentBag<QueueEntry> _queue = [];

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
        foreach (var uri in _options.Urls)
        {
            _queue.Add(new(uri, true));
        }

        this.UpdateUi(true);

        try
        {
            new Thread(UpdateUiThreadFunc).Start();
        }
        catch
        {
            // Do nothing.
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            var entries = _queue
                .Where(n => !n.Finished.HasValue)
                .ToList();

            if (entries.Count == 0)
            {
                break;
            }

            try
            {
                await Parallel.ForEachAsync(entries, cancellationToken, async (entry, token) =>
                    await this.HandleQueueEntry(entry, token));
            }
            catch (Exception)
            {
                // Do nothing.
            }
        }

        while (_activeRequests > 0)
        {
            await Task.Delay(100, cancellationToken);
        }
        
        Console.ResetColor();
        return;

        // UI update thread.
        async void UpdateUiThreadFunc()
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    this.UpdateUi(false);
                    await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
                }
            }
            catch
            {
                // Do nothing.
            }
        }
    }

    /// <summary>
    /// <inheritdoc cref="ICrawlerService.SetupPlaywright"/>
    /// </summary>
    public async Task<bool> SetupPlaywright()
    {
        try
        {
            ConsoleEx.Write("Setting up Playwright..");
            
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
            ConsoleEx.WriteError(ex.Message);
            return false;
        }
    }
    
    /// <summary>
    /// Update/redraw the UI.
    /// </summary>
    /// <param name="redraw">Whether to redraw the whole UI.</param>
    public void UpdateUi(bool redraw)
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

        int top;

        if (redraw)
        {
            Console.CursorVisible = false;
            Console.ResetColor();
            Console.Clear();
            
            ConsoleEx.WriteAt(0, 0, 
                ConsoleColor.White, 
                Program.NameAndVersion);
            
            ConsoleEx.WriteAt(0, 1, ConsoleColor.Gray,
                "Press ",
                ConsoleColor.Blue,
                "CTRL+C ",
                ConsoleColor.Gray,
                "to abort");
            
            ConsoleEx.WriteAt(0, 3, ConsoleColor.Gray, "Started");
            ConsoleEx.WriteAt(0, 4, ConsoleColor.Gray, "Duration");
            ConsoleEx.WriteAt(0, 5, ConsoleColor.Gray, "Progress");
            ConsoleEx.WriteAt(0, 6, ConsoleColor.Gray, "Requests");
            
            ConsoleEx.WriteAt(10, 8, ConsoleColor.Gray, "< 450 ms");
            ConsoleEx.WriteAt(10, 9, ConsoleColor.Gray, "> 450 ms < 900 ms");
            ConsoleEx.WriteAt(10, 10, ConsoleColor.Gray, "> 900 ms");
            
            lock (_responseTypes)
            {
                top = 11;

                foreach (var type in _responseTypes)
                {
                    ConsoleEx.WriteAt(10, ++top, ConsoleColor.Gray, type.Key);
                }
            }
            
            ConsoleEx.WriteAt(10, 3, 
                ConsoleColor.DarkGreen, 
                $"{_started:yyyy-MM-dd} {_started:HH:mm}");
        }
        
        // Update duration.
        var duration = DateTimeOffset.Now - _started;

        ConsoleEx.WriteAt(10, 4, 
            ConsoleColor.DarkGreen, 
            this.GetFormattedTimeSpan(duration));

        // Update progress.
        var finished = _queue.Count(n => n.Finished.HasValue);
        var percent = finished < _queue.Count
            ? (int)(100.00 / _queue.Count * finished)
            : 100;
        
        ConsoleEx.WriteAt(10, 5, 
            ConsoleColor.DarkGreen, 
            $"{finished} ({percent}%) of {_queue.Count}");

        // Update requests per. second.
        var requestsPerSecond = finished > 0
            ? finished / duration.TotalSeconds
            : 0;
        
        ConsoleEx.WriteAt(10, 6, 
            ConsoleColor.DarkGreen, 
            $"~{(int)requestsPerSecond}/s        ");

        // Update response times.
        lock (_responseTimes)
        {
            foreach (var responseTimeRange in Enum.GetValues<ResponseTimeRange>())
            {
                var index = (int)responseTimeRange;
                var value = _responseTimes[responseTimeRange];
                var count = value.ToString();

                count = new string(' ', 8 - count.Length) + count;
                
                ConsoleEx.WriteAt(0, 8 + index, 
                    value > 0 ? ConsoleColor.DarkYellow : ConsoleColor.DarkGray, 
                    count);
            }
        }

        // Update response types.
        top = 11;

        lock (_responseTypes)
        {
            foreach (var responseType in _responseTypes.OrderBy(n => n.Key))
            {
                var count = responseType.Value.ToString();
            
                count = new string(' ', 8 - count.Length) + count;
                
                ConsoleEx.WriteAt(0, ++top, 
                    ConsoleColor.DarkCyan, 
                    count);
            }            
        }
    }

    /// <summary>
    /// <inheritdoc cref="ICrawlerService.WriteQueueToDisk"/>
    /// </summary>
    public async Task WriteQueueToDisk()
    {
        var path = Path.Combine(
            Directory.GetCurrentDirectory(),
            $"queue-{_started:yyyy-MM-dd-HH-mm-ss}.json");

        int top; 

        lock (_responseTypes)
        {
            top = 13 + _responseTypes.Count;
        }

        try
        {
            ConsoleEx.WriteAt(0, top,
                "Writing queue to ",
                ConsoleColor.Yellow,
                path,
                Environment.NewLine,
                Environment.NewLine);
            
            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(
                stream,
                _queue.OrderBy(n => n.Added),
                _serializerOptions);
        }
        catch (Exception ex)
        {
            ConsoleEx.WriteError(ex.Message);
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
        Interlocked.Increment(ref _activeRequests);
        
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        
        entry.Attempts++;

        string responseType;
        
        try
        {
            string? contentType;
            int statusCode;
            Stopwatch watch;
            
            if (entry.PlaywrightRequest)
            {
                var page = await this.Browser!.NewPageAsync();
                var gotoOptions = new PageGotoOptions
                {
                    Timeout = _options.RequestTimeout,
                    WaitUntil = WaitUntilState.DOMContentLoaded
                };

                watch = Stopwatch.StartNew();
                
                var res = await page.GotoAsync(entry.Url.ToString(), gotoOptions)
                          ?? throw new Exception($"Unable to get a valid HTTP response from {entry.Url}");
                
                watch.Stop();

                statusCode = res.Status;
                contentType = await res.HeaderValueAsync("content-type");
                
                var isHtml = contentType?.Contains("text/html", StringComparison.InvariantCultureIgnoreCase);

                if (isHtml is true)
                {
                    await this.ParseResponseContent(entry, page);
                }
            }
            else
            {
                using var client = new HttpClient();
                
                client.Timeout = TimeSpan.FromMilliseconds(_options.RequestTimeout);

                watch = Stopwatch.StartNew();

                var res = await client.GetAsync(entry.Url, cancellationToken);
                
                watch.Stop();

                contentType = res.Content.Headers.ContentType?.MediaType;
                statusCode = (int)res.StatusCode;
            }

            var statusDescription = Tools.GetStatusDescription(statusCode);

            if (statusCode is >= 200 and <= 300)
            {
                entry.Finished = DateTimeOffset.Now;
            }
            
            entry.Responses.Add(
                new()
                {
                    ContentType = contentType,
                    StatusCode = statusCode,
                    StatusDescription = statusDescription,
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

            responseType = $"{statusCode} {statusDescription}";
        }
        catch (TimeoutException)
        {
            responseType = "ERR_TIMEOUT";
            entry.Errors.Add(new RequestError($"Request timeout after {(int)(_options.RequestTimeout / 1000)} second(s)."));
        }
        catch (Exception ex)
        {
            if (ex.Message.StartsWith("net::"))
            {
                responseType = ex.Message[5..];

                var len = responseType.IndexOf(' ');

                if (len > -1)
                {
                    responseType = responseType[..len];
                }
            }
            else
            {
                responseType = ex.Message;
            }
            
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

        Interlocked.Decrement(ref _activeRequests);
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
                    _queue.Add(new QueueEntry(uri, tag == "a"));
                }
            }
        }
    }

    #endregion
}