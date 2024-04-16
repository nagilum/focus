namespace Focus;

public static class ConsoleEx
{
    /// <summary>
    /// Lock object.
    /// </summary>
    private static readonly object ConsoleLock = new();
    
    /// <summary>
    /// Write objects to console.
    /// </summary>
    /// <param name="objects">Objects to write.</param>
    public static void Write(params object[] objects)
    {
        lock (ConsoleLock)
        {
            foreach (var obj in objects)
            {
                switch (obj)
                {
                    case ConsoleColor cc:
                        Console.ForegroundColor = cc;
                        break;
                    
                    case 0x00:
                        Console.ResetColor();
                        break;
                    
                    default:
                        Console.Write(obj.ToString());
                        break;
                }
            }
            
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Write objects to console as error.
    /// </summary>
    /// <param name="objects"></param>
    public static void WriteError(params object[] objects)
    {
        var list = new List<object>
        {
            ConsoleColor.Red,
            "Error",
            0x00,
            ": "
        };

        list.AddRange(objects);
        list.Add(Environment.NewLine);

        Write(list.ToArray());
    }
    
    /// <summary>
    /// Write objects to console.
    /// </summary>
    /// <param name="left">Left position.</param>
    /// <param name="top">Top position.</param>
    /// <param name="objects">Objects to write.</param>
    public static void WriteAt(int left, int top, params object[] objects)
    {
        lock (ConsoleLock)
        {
            Console.CursorLeft = left;
            Console.CursorTop = top;

            foreach (var obj in objects)
            {
                switch (obj)
                {
                    case ConsoleColor cc:
                        Console.ForegroundColor = cc;
                        break;
                    
                    case 0x00:
                        Console.ResetColor();
                        break;
                    
                    default:
                        Console.Write(obj.ToString());
                        break;
                }
            }
            
            Console.ResetColor();
        }
    }
}