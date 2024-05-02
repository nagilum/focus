using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Focus.Models;
using Focus.Models.Interfaces;
using HtmlAgilityPack;
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
        Converters = { new JsonStringEnumConverter() },
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
            _queue.Add(new(uri));
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
        var install = false;

        try
        {
            // If this fails, Playwright is most likely not installed.
            var playwright = await Playwright.CreateAsync();
            _ = await playwright.Chromium.LaunchAsync();
        }
        catch
        {
            ConsoleEx.Write(
                "Playwright is not installed, but is required for this app.",
                Environment.NewLine,
                "Do you wish to install it? Press ",
                ConsoleColor.Green,
                "Y ",
                0x00,
                "for yes. Any other key will cancel.",
                Environment.NewLine);

            var key = Console.ReadKey(true);

            if (key.KeyChar is 'y' or 'Y')
            {
                install = true;
            }
            else
            {
                return false;
            }
        }

        try
        {
            if (install)
            {
                ConsoleEx.WriteLine("Installing Playwright..");
                Microsoft.Playwright.Program.Main(["install"]);
            }

            ConsoleEx.WriteLine("Setting up Playwright..");

            var playwright = await Playwright.CreateAsync();

            this.Browser = _options.RenderingEngine switch
            {
                RenderingEngine.Chromium => await playwright.Chromium.LaunchAsync(),
                RenderingEngine.Firefox => await playwright.Firefox.LaunchAsync(),
                RenderingEngine.Webkit => await playwright.Webkit.LaunchAsync(),
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
            ConsoleEx.WriteAt(0, 6, ConsoleColor.Gray, "In Queue");
            ConsoleEx.WriteAt(0, 7, ConsoleColor.Gray, "Requests");

            ConsoleEx.WriteAt(10, 9, ConsoleColor.Gray, "< 450 ms");
            ConsoleEx.WriteAt(10, 10, ConsoleColor.Gray, "> 450 ms < 900 ms");
            ConsoleEx.WriteAt(10, 11, ConsoleColor.Gray, "> 900 ms");

            lock (_responseTypes)
            {
                top = 12;

                foreach (var type in _responseTypes.OrderBy(n => n.Key))
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
        var finished =
            _queue.Count(n => n.Attempts == 1) +
            _queue.Count(n => n.Attempts == 2) * 2 +
            _queue.Count(n => n.Attempts == 3) * 3;

        var total = _queue.Count * 3;
        var percent = finished < total
            ? (int)(100.00 / total * finished)
            : 100;

        ConsoleEx.WriteAt(10, 5,
            ConsoleColor.DarkGreen,
            $"{finished} ({percent}%) of {total}                 ");
        
        // Update URLs in queue.
        ConsoleEx.WriteAt(10, 6,
            ConsoleColor.DarkGreen,
            _queue.Count);

        // Update requests per. second.
        var requestsPerSecond = finished > 0
            ? finished / duration.TotalSeconds
            : 0;

        ConsoleEx.WriteAt(10, 7,
            ConsoleColor.DarkGreen,
            $"{requestsPerSecond:0.00}/s        ");

        // Update response times.
        lock (_responseTimes)
        {
            foreach (var responseTimeRange in Enum.GetValues<ResponseTimeRange>())
            {
                var index = (int)responseTimeRange;
                var value = _responseTimes[responseTimeRange];
                var count = value.ToString();

                count = new string(' ', 8 - count.Length) + count;

                ConsoleEx.WriteAt(0, 9 + index,
                    value > 0 ? ConsoleColor.DarkYellow : ConsoleColor.DarkGray,
                    count);
            }
        }

        // Update response types.
        top = 12;

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
            top = 14 + _responseTypes.Count;
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

    #region Request handling

    /// <summary>
    /// Crawl the given queue entry and update response data.
    /// </summary>
    /// <param name="entry">Queue entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task HandleQueueEntry(QueueEntry entry, CancellationToken cancellationToken)
    {
        entry.Attempts++;
        entry.Started ??= DateTimeOffset.Now;
        
        await this.PerformHttpClientRequest(entry, cancellationToken);
        await this.PerformPlaywrightRequest(entry, cancellationToken);

        if (entry.Attempts == 3)
        {
            entry.Finished = DateTimeOffset.Now;    
        }
    }

    /// <summary>
    /// Perform a HTTP client request.
    /// </summary>
    /// <param name="entry">Queue entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task PerformHttpClientRequest(IQueueEntry entry, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        
        string responseType;

        try
        {
            using var client = new HttpClient();

            client.Timeout = _options.RequestTimeout > 0
                ? TimeSpan.FromMilliseconds(_options.RequestTimeout)
                : Timeout.InfiniteTimeSpan;

            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
            client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en"));
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(Program.Name, Program.Version));

            var watch = Stopwatch.StartNew();

            var res = await client.GetAsync(entry.Url, cancellationToken);

            watch.Stop();
            
            // Add response data.
            var contentType = res.Content.Headers.ContentType?.MediaType;
            var statusCode = (int)res.StatusCode;
            var statusDescription = Tools.GetStatusDescription(statusCode);

            var response = new Response
            {
                ContentType = contentType,
                RequestType = RequestType.HttpClient,
                StatusCode = statusCode,
                StatusDescription = statusDescription,
                Time = watch.ElapsedMilliseconds
            };

            foreach (var (key, value) in res.Headers)
            {
                if (!response.Headers.ContainsKey(key))
                {
                    response.Headers.Add(key, string.Join(";", value));    
                }
            }

            foreach (var (key, value) in res.Content.Headers)
            {
                if (!response.Headers.ContainsKey(key))
                {
                    response.Headers.Add(key, string.Join(";", value));    
                }
            }
            
            entry.Responses.Add(response);
            
            // Increment response time range.
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
            
            // Add redirect URL to queue.
            if (res.Headers.Location is not null &&
                entry.Url.IsBaseOf(res.Headers.Location))
            {
                var url = res.Headers.Location.ToString();
                var alreadyAdded = _queue.Any(n => n.Url.ToString() == url);

                if (!alreadyAdded)
                {
                    _queue.Add(new QueueEntry(res.Headers.Location));
                }
            }
            
            // Set response type.
            responseType = $"{statusCode} {statusDescription}";
            
            // Parse HTML for new links.
            var isHtml = contentType?.Contains("text/html", StringComparison.InvariantCultureIgnoreCase);

            if (isHtml is true)
            {
                await this.ParseResponseContent(entry, res, cancellationToken);
            }
        }
        catch (TaskCanceledException)
        {
            return;
        }
        catch (TimeoutException)
        {
            responseType = "TIMEOUT";
            
            entry.Errors.Add(
                new(
                    typeof(TimeoutException).ToString(),
                    $"Request timeout after {(int)(_options.RequestTimeout / 1000)} second(s)."));
        }
        catch (Exception ex)
        {
            responseType = ex.GetType().Name.ToUpper();
            entry.Errors.Add(new(ex));
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
                this.ResponseTypes = _responseTypes.Count;
            }
        }
    }

    /// <summary>
    /// Perform a HTTP client request.
    /// </summary>
    /// <param name="entry">Queue entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task PerformPlaywrightRequest(IQueueEntry entry, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        string responseType;

        try
        {
            var newPageOptions = new BrowserNewPageOptions
            {
                ExtraHTTPHeaders = new Dictionary<string, string>
                {
                    { "Accept-Language", "en" }
                }
            };

            var page = await this.Browser!.NewPageAsync(newPageOptions);
            var gotoOptions = new PageGotoOptions
            {
                Timeout = _options.RequestTimeout,
                WaitUntil = WaitUntilState.DOMContentLoaded
            };

            var watch = Stopwatch.StartNew();

            var res = await page.GotoAsync(entry.Url.ToString(), gotoOptions)
                      ?? throw new Exception($"Unable to get a valid HTTP response from {entry.Url}");

            watch.Stop();

            // Add response data.
            var contentType = await res.HeaderValueAsync("content-type");
            var statusCode = res.Status;
            var statusDescription = Tools.GetStatusDescription(statusCode);

            var response = new Response
            {
                ContentType = contentType,
                RequestType = RequestType.Playwright,
                StatusCode = statusCode,
                StatusDescription = statusDescription,
                Time = watch.ElapsedMilliseconds
            };

            foreach (var (key, value) in res.Headers)
            {
                response.Headers.TryAdd(key, value);
            }

            entry.Responses.Add(response);

            // Increment response time range.
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

            // Add redirect URL to queue.
            var redirectUrl = await res.HeaderValueAsync("location");

            if (redirectUrl is not null &&
                Uri.TryCreate(redirectUrl, UriKind.Absolute, out var redirectUri) &&
                entry.Url.IsBaseOf(redirectUri))
            {
                var alreadyAdded = _queue.Any(n => n.Url.ToString() == redirectUrl);

                if (!alreadyAdded)
                {
                    _queue.Add(new QueueEntry(redirectUri));
                }
            }

            // Set response type.
            responseType = $"{statusCode} {statusDescription}";

            // Parse HTML for new links.
            var isHtml = contentType?.Contains("text/html", StringComparison.InvariantCultureIgnoreCase);

            if (isHtml is true)
            {
                await this.ParseResponseContent(entry, page);
            }

            // Close page.
            await page.CloseAsync();
        }
        catch (PlaywrightException ex)
        {
            if (ex.Message.StartsWith("net::ERR_ABORTED"))
            {
                responseType = "SKIPPED";
                entry.Errors.Add(new("Skipped", "Skipped because Playwright could not render the URL."));
            }
            else
            {
                responseType = ex.GetType().Name.ToUpper();
                entry.Errors.Add(new(ex));    
            }
        }
        catch (TaskCanceledException)
        {
            return;
        }
        catch (TimeoutException)
        {
            responseType = "TIMEOUT";
            
            entry.Errors.Add(
                new(
                    typeof(TimeoutException).ToString(),
                    $"Request timeout after {(int)(_options.RequestTimeout / 1000)} second(s)."));
        }
        catch (Exception ex)
        {
            responseType = ex.GetType().Name.ToUpper();
            entry.Errors.Add(new(ex));
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
                this.ResponseTypes = _responseTypes.Count;
            }
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
    /// Extract new URLs to crawl.
    /// </summary>
    /// <param name="entry">Queue entry.</param>
    /// <param name="res">HTTP response message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task ParseResponseContent(
        IQueueEntry entry, 
        HttpResponseMessage res, 
        CancellationToken cancellationToken)
    {
        HtmlDocument doc;

        try
        {
            var bytes = await res.Content.ReadAsByteArrayAsync(cancellationToken);
            var html = Encoding.UTF8.GetString(bytes);

            doc = new HtmlDocument();
            doc.LoadHtml(html);
        }
        catch
        {
            return;
        }
        
        var selectors = new Dictionary<string, string>
        {
            { "a", "href" },
            { "img", "src" },
            { "link", "href" },
            { "script", "src" }
        };

        foreach (var (tag, attr) in selectors)
        {
            var nodes = doc.DocumentNode.SelectNodes($"//{tag}[@{attr}]");

            foreach (var node in nodes)
            {
                var url = node.GetAttributeValue(attr, null);
                
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

    #endregion
}