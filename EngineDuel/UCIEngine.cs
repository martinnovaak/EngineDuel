using System.Diagnostics;

namespace EngineDuel;

class UCIEngine
{
    private Process process;
    private Stopwatch stopwatch;
    private int time = 8000;
    private int increment = 80;
    private string name;

    public UCIEngine(string enginePath)
    {
        process = new Process();
        process.StartInfo.FileName = enginePath;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardInput = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.CreateNoWindow = true;
        process.Start();
        
        stopwatch = new();
        InitializeEngine();
    }

    public string getName()
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

    public string GetBestMove()
    {
        SendCommand($"go wtime {time} btime {time} winc{increment} binc{increment}");
        
        stopwatch.Start();
        
        string bestMove = WaitForBestMove();
        
        stopwatch.Stop();
        
        time += (increment - (int)stopwatch.ElapsedMilliseconds);

        stopwatch.Reset();
        
        return bestMove;
    }

    private void SendCommand(string command)
    {
        process.StandardInput.WriteLine(command);
    }

    private bool WaitForResponse(string expectedResponse)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        int timeout = 5;
        
        string? response;
        do
        {
            response = process.StandardOutput.ReadLine();
            if (response != null && response.Contains("id name"))
            {
                name = response.ExtractName();
            }
        } while (response != null && !response.Contains(expectedResponse) && stopwatch.Elapsed.TotalSeconds < timeout);

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
        SendCommand("stop");
        
        SendCommand("quit");
        
        WaitForResponse("uciok");
        
        process.StandardInput.Close();
    }
}
