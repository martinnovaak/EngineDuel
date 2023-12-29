using System.Diagnostics;

namespace EngineDuel;

public static class StringExtensions
{
    public static string ExtractName(this string input) => 
        input.Split(' ').Skip(2).FirstOrDefault() ?? "unknown";
}

class UCIEngine
{
    private Process process;
    private Stopwatch stopwatch;
    private int time = 10000;
    private int increment = 100;
    private string name;

    public UCIEngine(string enginePath, int initialTime, int timeIncrement)
    {
        time = initialTime;
        increment = timeIncrement;
        
        process = new Process();
        process.StartInfo.FileName = enginePath;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardInput = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.CreateNoWindow = true;
        
        process.Start();
        
        process.PriorityClass = ProcessPriorityClass.High; // Set the priority to High
        
        stopwatch = new();
        InitializeEngine();
    }

    public string GetName()
    {
        return name;
    }

    private void InitializeEngine()
    {
        SendCommand("uci");
        if (!WaitForResponse("uciok"))
        {
            Console.WriteLine("Engine did not respond.\n");
        } 
    }

    public void SetPosition(string fen, string moves)
    {
        if (moves != "")
        {
            fen += " moves " + moves;
        }
        SendCommand($"position {fen}");
    }

    public void SetOption(string optionID, double value)
    {
        SendCommand($"setoption name {optionID} value {value}");
    }

    public string GetBestMove()
    {
        
        SendCommand($"go wtime {time} btime {time} winc{increment} binc{increment}");
        //TimeSpan startProcessorTime = process.TotalProcessorTime;
        stopwatch.Start();
        
        string bestMove = WaitForBestMove();
        
        stopwatch.Stop();
        
        //TimeSpan endProcessorTime = process.TotalProcessorTime;
        
        //TimeSpan cpuTimeUsed = endProcessorTime - startProcessorTime;
        //if (Math.Abs(cpuTimeUsed.TotalMilliseconds - stopwatch.ElapsedMilliseconds) > 50.0) 
        //Console.WriteLine($"{cpuTimeUsed.TotalMilliseconds}, {stopwatch.ElapsedMilliseconds}");
        
        time += (increment - (int)stopwatch.ElapsedMilliseconds);

        stopwatch.Reset();
        
        return bestMove;
    }

    public bool TimeLeft()
    {
        return time > 0;
    }

    private void SendCommand(string command)
    {
        process.StandardInput.WriteLine(command);
    }

    private bool WaitForResponse(string expectedResponse)
    {
        Stopwatch timeoutStopwatch = Stopwatch.StartNew();
        int timeout = 5;
        
        string? response;
        do
        {
            response = process.StandardOutput.ReadLine();
            if (response != null && response.Contains("id name"))
            {
                name = response.ExtractName();
            }
        } while (response != null && !response.Contains(expectedResponse) && timeoutStopwatch.Elapsed.TotalSeconds < timeout);

        return response != null;
    }

    private string WaitForBestMove()
    {
        // Wait for the engine to respond with the best move
        string response;
        do
        {
            response = process.StandardOutput.ReadLine();
            // Check if the response contains "bestmove" to identify the line with the best move
            if (response != null && response.StartsWith("bestmove"))
            {
                // Extract the best move from the response
                string[] parts = response.Split(' ');
                if (parts.Length >= 2)
                {
                    return parts[1];
                }
            }
        } while (response != null);

        return null;
    }
    
    // Stop engine calculation
    public void StopEngine()
    {
        SendCommand("stop");
    }

    // Shut down the engine
    public void QuitEngine()
    {
        try
        {
            SendCommand("quit");
            process.WaitForExit();
            process.Close();
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Error while quitting engine: {ex.Message}");
        
            // Optionally, you can attempt to clear or reset the process.
            // For example, you may create a new process instance if needed.
            // process = new Process();
        }
    }
}
