using System.Runtime.InteropServices;
using EngineDuel;

public static class StringExtensions
{
    public static string ExtractName(this string input) => 
        input.Split(' ').Skip(2).FirstOrDefault() ?? "unknown";
}

public struct GameResult
{
    private int wins;
    private int draws;
    private int loses;
    
    public int Wins => wins;
    public int Draws => draws;
    public int Loses => loses;
    
    public void IncrementWins() => Interlocked.Increment(ref wins);
    public void IncrementDraws() => Interlocked.Increment(ref draws);
    public void IncrementLoses() => Interlocked.Increment(ref loses);
}

class ChessGame
{
    private static CancellationTokenSource cancelToken = new ();
    private static CountdownEvent countdownEvent = new(16);
    private static int roundCounter;
    private static GameResult gameResult;
    
    private static readonly object fileLock = new ();
    private static readonly object consoleLock = new ();
    
    enum Color { White, Black }
    enum GameState { Ongoing, Draw, Checkmate, Error }
    
    [DllImport("chesslib.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern GameState process_moves(string moves);

    private static void SaveResult(int result)
    {
        switch (result)
        {
            case 1:
                gameResult.IncrementWins();
                break;
            case 0:
                gameResult.IncrementDraws();
                break;
            case -1:
                gameResult.IncrementLoses();
                break;
        }
    }
    
    static int Finishgame(GameState state, Color color, ref PGN pgn)
    {
        string result = state switch
        {
            GameState.Draw => "1/2-1/2",
            GameState.Checkmate => $"{(color == Color.White? "1-0" : "0-1")}",
            GameState.Error => $"{(color == Color.White? "0-1" : "1-0")}",
            _ => "Unknown State",
        };
        
        string filePath = "output.pgn";
        int currentRound = Interlocked.Increment(ref roundCounter);
        string pgnMoves = pgn.GetGame(result, currentRound);
        
        // Use lock to synchronize console writes
        lock (fileLock)
        {
            File.AppendAllText(filePath, pgnMoves);
        }
        
        lock (consoleLock)
        {
            Console.WriteLine($"Wins: {gameResult.Wins}, draws: {gameResult.Draws}, loses: {gameResult.Loses}");
        }

        return result switch
        {
            "1/2-1/2" => 0,
            "1-0" => 1,
            "0-1" => -1,
        };
    }
    
    private static void RunChessMatches(string engine1Path, string engine2Path)
    {
        for (int i = 0; i < 100; i++)
        {
            // Queue threads for running chess matches
            ThreadPool.QueueUserWorkItem(_ =>
            {
                SaveResult(ChessMatch(engine1Path, engine2Path));
                SaveResult(-ChessMatch(engine2Path, engine1Path));
                countdownEvent.Signal();
            });

            ThreadPool.QueueUserWorkItem(_ =>
            {
                SaveResult(-ChessMatch(engine2Path, engine1Path));
                SaveResult(ChessMatch(engine1Path, engine2Path));
                countdownEvent.Signal();
            });
        }
    }

    static int ChessMatch(string whiteEnginePath, string blackEnginePath)
    {
        // Initialize communication with the engines
        UCIEngine engine1 = new UCIEngine(whiteEnginePath);
        UCIEngine engine2 = new UCIEngine(blackEnginePath);

        string moves = "";
        GameState state;
        int result = 0;

        PGN pgn = new(engine1.getName(), engine2.getName());
        // Game loop
        while (true)
        {
            if (cancelToken.IsCancellationRequested)
            {
                engine1.QuitEngine();
                engine2.QuitEngine(); 
                break;
            }
            
            engine1.SetPosition("startpos", moves);
            Task<string> moveFromEngine1Task = Task.Run(() => engine1.GetBestMove());
            string moveFromEngine1 = moveFromEngine1Task.Result; 
            pgn.PlayMove(moveFromEngine1);

            moves += $" {moveFromEngine1} ";
            state = process_moves(moves);
            
            if (state != GameState.Ongoing)
            {
                result = Finishgame(state, Color.White, ref pgn);
                engine1.QuitEngine();
                engine2.QuitEngine(); 
                break;
            }

            engine2.SetPosition("startpos", moves);
            Task<string> moveFromEngine2Task = Task.Run(() => engine2.GetBestMove());
            string moveFromEngine2 = moveFromEngine2Task.Result;

            pgn.PlayMove(moveFromEngine2);
            
            moves += $" {moveFromEngine2} ";

            state = process_moves(moves);
            if (state != GameState.Ongoing)
            {
                result = Finishgame(state, Color.Black, ref pgn);
                engine1.QuitEngine(); 
                engine2.QuitEngine(); 
                break;
            }
        }

        return result;
    }
    
    static void Main()
    {
        string engine1Path = "engine1.exe";
        string engine2Path = "engine2.exe";

        ThreadPool.SetMaxThreads(12, 12);
        RunChessMatches(engine1Path, engine2Path);
        countdownEvent.Wait();
        cancelToken.Cancel();
    }
}
