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
    public const string Version = "0.2-beta";

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
            ConsoleEx.WriteAt(6, 1, ConsoleColor.Red, "CTRL+C");
            
            source.Cancel();
            e.Cancel = true;
        };

        var crawler = new CrawlerService(options);

        if (!await crawler.SetupPlaywright())
        {
            return;
        } 

        await crawler.Run(source.Token);
        await crawler.DisposePlaywright();
        
        await source.CancelAsync();

        crawler.UpdateUi(true);
        
        await crawler.WriteQueueToDisk();
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
                case "-e":
                    if (i == args.Count - 1)
                    {
                        ConsoleEx.WriteError(
                            ConsoleColor.Yellow,
                            "-e ",
                            0x00,
                            "must be followed by the name of the rendering engine to use.");
                        
                        return false;
                    }

                    switch (args[i + 1].ToLower())
                    {
                        case "chromium":
                            options.RenderingEngine = RenderingEngine.Chromium;
                            break;
                        
                        case "firefox":
                            options.RenderingEngine = RenderingEngine.Firefox;
                            break;
                        
                        case "webkit":
                            options.RenderingEngine = RenderingEngine.Webkit;
                            break;
                        
                        default:
                            ConsoleEx.WriteError(
                                "Invalid value for ",
                                ConsoleColor.Yellow,
                                "-e",
                                0x00,
                                ": ",
                                ConsoleColor.DarkRed,
                                args[i + 1]);
                            
                            return false;
                    }

                    skip = true;
                    break;
                
                case "-r":
                    if (i == args.Count - 1)
                    {
                        ConsoleEx.WriteError(
                            ConsoleColor.Yellow,
                            "-r ",
                            0x00,
                            "must be followed by a number of attempts.");
                        
                        return false;
                    }

                    if (!int.TryParse(args[i + 1], out var attempts) ||
                        attempts < 1)
                    {
                        ConsoleEx.WriteError(
                            "Invalid value for ",
                            ConsoleColor.Yellow,
                            "-r",
                            0x00,
                            ": ",
                            ConsoleColor.DarkRed,
                            args[i + 1]);
                        
                        return false;
                    }

                    options.MaxRetryAttempts = attempts;
                    skip = true;
                    break;
                
                case "-t":
                    if (i == args.Count - 1)
                    {
                        ConsoleEx.WriteError(
                            ConsoleColor.Yellow,
                            "-t ",
                            0x00,
                            "must be followed by a number of seconds.");
                        
                        return false;
                    }

                    if (!int.TryParse(args[i + 1], out var seconds) ||
                        seconds < -1)
                    {
                        ConsoleEx.WriteError(
                            "Invalid value for ",
                            ConsoleColor.Yellow,
                            "-t",
                            0x00,
                            ": ",
                            ConsoleColor.DarkRed,
                            args[i + 1]);
                        
                        return false;
                    }

                    options.RequestTimeout = seconds > 0 ? seconds * 1000 : 0;
                    skip = true;
                    break;

                default:
                    if (!Uri.TryCreate(args[i], UriKind.Absolute, out var uri))
                    {
                        ConsoleEx.WriteError(
                            "Invalid URL: ",
                            ConsoleColor.DarkRed,
                            args[i]);
                        
                        return false;
                    }

                    if (!options.Urls.Contains(uri))
                    {
                        options.Urls.Add(uri);
                    }

                    break;
            }
        }

        if (options.Urls.Count > 0)
        {
            return true;
        }

        ConsoleEx.WriteError("You must add at least one URL to scan.");
        return false;
    }

    /// <summary>
    /// Show program usage and options.
    /// </summary>
    private static void ShowProgramUsage()
    {
        ConsoleEx.WriteLine(ConsoleColor.White, NameAndVersion);
        ConsoleEx.WriteLine("Crawl a site and log all responses.");
        ConsoleEx.WriteLine();
        ConsoleEx.WriteLine("Usage:");
        
        ConsoleEx.WriteLine(
            ConsoleColor.Yellow,
            "  focus ",
            ConsoleColor.White,
            "<url> ",
            0x00,
            "[<options>]");
        
        ConsoleEx.WriteLine();
        ConsoleEx.WriteLine("Options:");
        
        ConsoleEx.WriteLine(
            ConsoleColor.Yellow,
            "  -e ",
            ConsoleColor.White,
            "<name>       ",
            0x00,
            "Set rendering engine. Options are ",
            ConsoleColor.Yellow,
            "chromium",
            0x00,
            ", ",
            ConsoleColor.Yellow,
            "firefox",
            0x00,
            ", and ",
            ConsoleColor.Yellow,
            "webkit",
            0x00,
            ". Defaults to ",
            ConsoleColor.Yellow,
            "chromium",
            0x00,
            ".");
        
        ConsoleEx.WriteLine(
            ConsoleColor.Yellow,
            "  -r ",
            ConsoleColor.White,
            "<attempts>   ",
            0x00,
            "Retries all failed (non 2xx) requests n times. Defaults to ",
            ConsoleColor.Yellow,
            "0",
            0x00,
            ".");
        
        ConsoleEx.WriteLine(
            ConsoleColor.Yellow,
            "  -t ",
            ConsoleColor.White,
            "<seconds>    ",
            0x00,
            "Set request timeout, in seconds. Set to ",
            ConsoleColor.Yellow,
            "0",
            0x00,
            " to disable. Defaults to ",
            ConsoleColor.Yellow,
            "10",
            0x00,
            ".");
        
        ConsoleEx.WriteLine();
        ConsoleEx.WriteLine("The URL parameter is repeatable.");
        ConsoleEx.WriteLine("Source and documentation available at https://github.com/nagilum/focus");
    }
}