using System.Diagnostics;
using System.Runtime.InteropServices;
using engine_test;

public static class StringExtensions
{
    public static string ExtractName(this string input) => 
        input.Split(' ').Skip(2).FirstOrDefault() ?? "unknown";
}

class ChessGame
{
    private static CountdownEvent countdownEvent = new(2);
    private static readonly object consoleLock = new ();
    static int roundCounter = 0;
    
    enum Color
    {
        White, Black
    }
    
    enum GameState
    {
        Ongoing, Draw, Checkmate, Error
    }
    
    [DllImport("chesslib.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern GameState process_moves(string moves);

    static void Finishgame(GameState state, Color color, ref PGN pgn)
    {
        string result = state switch
        {
            GameState.Draw => "1/2-1/2",
            GameState.Checkmate => $"{ (color == Color.White? "1-0" : "0-1") }",
            GameState.Error => $"{ (color == Color.White? "0-1" : "1-0") }",
            _ => "Unknown State",
        };
        
        string filePath = "output.pgn";
        int currentRound = Interlocked.Increment(ref roundCounter);
        string pgnMoves = pgn.getGame(result, currentRound);
        
        // Use lock to synchronize console writes
        lock (consoleLock)
        {
            File.AppendAllText(filePath, pgnMoves);
        }
    }
    
    static void Main()
    {
        string engine1Path = "engine1.exe";
        string engine2Path = "engine2.exe";
        
        ThreadPool.QueueUserWorkItem(_ => RunChessMatch(engine1Path, engine2Path));
        ThreadPool.QueueUserWorkItem(_ => RunChessMatch(engine2Path, engine1Path));
        
        countdownEvent.Wait();
    }
    
    static void RunChessMatch(string whiteEnginePath, string blackEnginePath)
    {
        ChessMatch(whiteEnginePath, blackEnginePath);
        countdownEvent.Signal();
    }

    static void ChessMatch(string whiteEnginePath, string blackEnginePath)
    {
        // Start the engines as processes
        Process engine1Process = StartEngineProcess(whiteEnginePath);
        Process engine2Process = StartEngineProcess(blackEnginePath);

        // Initialize communication with the engines
        UCIEngine engine1 = new UCIEngine(engine1Process);
        UCIEngine engine2 = new UCIEngine(engine2Process);

        string moves = "";
        GameState state;
        
        // Game loop
        while (true)
        {
            engine1.SetPosition("startpos", moves);
            Task<string> moveFromEngine1Task = Task.Run(() => engine1.GetBestMove());
            string moveFromEngine1 = moveFromEngine1Task.Result; 

            moves += $" {moveFromEngine1} ";

            state = process_moves(moves);
            if (state != GameState.Ongoing)
            {
                Finishgame(state, Color.White, moves);
                engine1.StopEngine(); 
                engine1.QuitEngine();
                engine2.StopEngine(); 
                engine2.QuitEngine(); 
                break;
            }

            engine2.SetPosition("startpos", moves);
            Task<string> moveFromEngine2Task = Task.Run(() => engine2.GetBestMove());
            string moveFromEngine2 = moveFromEngine2Task.Result;

            pgn.playMove(moveFromEngine2);
            
            moves += $" {moveFromEngine2} ";

            state = process_moves(moves);
            if (state != GameState.Ongoing)
            {
                lock (consoleLock)
                {
                    Console.WriteLine($"{engine1.getName()} - {engine2.getName()}");
                }
                Finishgame(state, Color.Black, ref pgn);
                engine1.StopEngine(); 
                engine1.QuitEngine(); 
                engine2.StopEngine();
                engine2.QuitEngine(); 
                break;
            }
        }
        
        engine1Process.Close();
        engine2Process.Close();
    }

    static Process StartEngineProcess(string enginePath)
    {
        Process process = new Process();
        process.StartInfo.FileName = enginePath;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardInput = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.CreateNoWindow = true;
        process.Start();

        return process;
    }
}

class UCIEngine
{
    private Process process;
    private Stopwatch stopwatch;
    private int time = 1000;
    private int increment = 10;
    private string name;

    public UCIEngine(Process process)
    {
        this.process = process;
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
        SendCommand("quit");
        
        WaitForResponse("uciok");
        
        process.StandardInput.Close();
    }
}
