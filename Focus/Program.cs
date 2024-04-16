using System.Diagnostics.CodeAnalysis;
using Focus.Models;
using Focus.Services;

namespace Focus;

internal static class Program
{
    /// <summary>
    /// Program name and version.
    /// </summary>
    public static string NameAndVersion => $"{Name} v{Version}";

    /// <summary>
    /// Program name.
    /// </summary>
    public const string Name = "Focus";

    /// <summary>
    /// Program version.
    /// </summary>
    public const string Version = "0.1-alpha";

    /// <summary>
    /// Init all the things..
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    private static async Task Main(string[] args)
    {
        if (args.Length == 0 ||
            args.Any(n => n is "-h" or "--help"))
        {
            ShowProgramUsage();
            return;
        }

        if (!ParseCmdArgs(args, out var options))
        {
            return;
        }

        var source = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            source.Cancel();
            e.Cancel = true;
        };

        var crawler = new CrawlerService(options);

        await crawler.Run(source.Token);
        
        Console.ResetColor();

        if (source.IsCancellationRequested)
        {
            Console.WriteLine("Aborted by user!");
        }
    }

    /// <summary>
    /// Parse command line arguments.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <param name="options">Parsed options.</param>
    /// <returns>Success.</returns>
    private static bool ParseCmdArgs(IReadOnlyList<string> args, [NotNullWhen(returnValue: true)] out Options? options)
    {
        options = new();

        var skip = false;

        for (var i = 0; i < args.Count; i++)
        {
            if (skip)
            {
                skip = false;
                continue;
            }

            switch (args[i])
            {
                case "-r":
                    if (i == args.Count - 1)
                    {
                        Console.WriteLine("Error: -r must be followed by a number of attempts.");
                        return false;
                    }

                    if (!int.TryParse(args[i + 1], out var attempts) ||
                        attempts < 1)
                    {
                        Console.WriteLine($"Error: Invalid value for -r: {args[i + 1]}");
                        return false;
                    }

                    options.MaxRetryAttempts = attempts;
                    skip = true;
                    break;
                
                case "-t":
                    if (i == args.Count - 1)
                    {
                        Console.WriteLine("Error: -t must be followed by a number of seconds.");
                        return false;
                    }

                    if (!int.TryParse(args[i + 1], out var seconds) ||
                        seconds < 0)
                    {
                        Console.WriteLine($"Error: Invalid value for -t: {args[i + 1]}");
                        return false;
                    }

                    options.RequestTimeout = TimeSpan.FromSeconds(seconds);
                    skip = true;
                    break;

                default:
                    if (!Uri.TryCreate(args[i], UriKind.Absolute, out var uri))
                    {
                        Console.WriteLine($"Error: Invalid URL: {args[i]}");
                        return false;
                    }

                    if (!options.Urls.Contains(uri))
                    {
                        options.Urls.Add(uri);
                    }

                    break;
            }
        }

        return true;
    }

    /// <summary>
    /// Show program usage and options.
    /// </summary>
    private static void ShowProgramUsage()
    {
        string[] lines =
        [
            NameAndVersion,
            "Crawl a site and test responses.",
            "",
            "Usage:",
            "  focus <url> [<options>]",
            "",
            "Options:",
            "  -r <attempts>   Retries all failed (non 2xx) requests n times. Defaults to 0.",
            "  -t <seconds>    Set request timeout, in seconds. Defaults to 10.",
            "",
            "The URL parameter is repeatable.",
            "",
            "Source and documentation available at https://github.com/nagilum/focus"
        ];

        foreach (var line in lines)
        {
            Console.WriteLine(line);
        }
    }
}