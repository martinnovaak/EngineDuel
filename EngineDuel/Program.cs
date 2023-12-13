using System.Collections.Concurrent;
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
    private static CountdownEvent countdownEvent = new(200);
    private static int roundCounter;
    private static GameResult gameResult;
    
    private static readonly object fileLock = new ();
    private static readonly object consoleLock = new ();

    private static ConcurrentStack<string> openings;

    private static SPRT sprt;
    
    enum Color { White, Black }
    enum GameState { Ongoing, Draw, Checkmate, Error, TimeOut }
    
    [DllImport("chesslib.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern GameState process_moves(string moves);

    public static bool IsMoveStringLegal(string move)
    {
        if (string.IsNullOrEmpty(move) || move.Length < 4 || move.Length > 5)
        {
            // Invalid if the string is null, empty, or less than 4 characters
            return false;
        }

        char fromFile = move[0];
        char fromRank = move[1];
        char toFile = move[2];
        char toRank = move[3];

        // Check if first and third letters are file letters (a-h)
        if (!"abcdefgh".Contains(fromFile) || !"abcdefgh".Contains(toFile))
        {
            return false;
        }

        // Check if second and fourth letters are rank letters (1-8)
        if (!"12345678".Contains(fromRank) || !"12345678".Contains(toRank))
        {
            return false;
        }

        // Check if it has five letters, and if so, the fifth letter is a promotion letter
        if (move.Length == 5)
        {
            char promotionLetter = move[4];
            if (!"qrbn".Contains(promotionLetter))
            {
                return false;
            }
        }

        // If all checks pass, the move is considered legal
        return true;
    }
    
    private static void SaveResult(int result)
    {
        if (cancelToken.IsCancellationRequested)
        {
            return;
        }
        
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

        //var sprtInstance = new SPRT(0.05, 0.05, 0, 5);
        var testResult = sprt.test(gameResult.Wins, gameResult.Draws, gameResult.Loses);

        if (testResult.Item1)
        {
            cancelToken.Cancel();
        }
        
        lock (consoleLock)
        {
            Console.WriteLine($"Wins: {gameResult.Wins}, draws: {gameResult.Draws}, loses: {gameResult.Loses}. {testResult.Item2}");
        }
    }
    
    static int Finishgame(GameState state, Color color, ref PGN pgn)
    {
        string result = (state, color) switch
        {
            (GameState.Draw, _) => "1/2-1/2",
            (GameState.Checkmate, Color.White) => "1-0",
            (GameState.Checkmate, Color.Black) => "0-1",
            (GameState.Error, Color.White) => "0-1",
            (GameState.Error, Color.Black) => "1-0",
            (GameState.TimeOut, Color.White) => "0-1",
            (GameState.TimeOut, Color.Black) => "1-0",
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

        return result switch
        {
            "1/2-1/2" => 0,
            "1-0" => 1,
            "0-1" => -1,
        };
    }
    
    private static void RunChessMatches(string engine1Path, string engine2Path)
    {
        for (int i = 0; i < 200; i++)
        {
            ThreadPool.QueueUserWorkItem(_ => ChessMatch(engine1Path, engine2Path, 1));
            ThreadPool.QueueUserWorkItem(_ => ChessMatch(engine2Path, engine1Path, -1));
        }
    }
    
    private static void ChessMatch(string whiteEnginePath, string blackEnginePath, int coefficient)
    {
        if (openings.Count < 10)
        {
            Database.GetRandomSample(openings, 20);
        }
        
        string gameOpening;
        if (!openings.TryPop(out gameOpening))
        {
            gameOpening = "";
        }

        int result1 = SingleGame(whiteEnginePath, blackEnginePath, gameOpening);
        SaveResult(coefficient * result1);

        int result2 = -SingleGame(blackEnginePath, whiteEnginePath, gameOpening);
        SaveResult(coefficient * result2);

        countdownEvent.Signal();
    }

    static int SingleGame(string whiteEnginePath, string blackEnginePath, string initialMoves)
    {
        // Initialize communication with the engines
        UCIEngine engine1 = new UCIEngine(whiteEnginePath);
        UCIEngine engine2 = new UCIEngine(blackEnginePath);

        string moves = initialMoves;
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

            moves += $" {moveFromEngine1} ";
            state = process_moves(moves);

            if (!engine1.TimeLeft())
            {
                state = GameState.TimeOut;
            }
            
            if(!IsMoveStringLegal(moveFromEngine1))
            {
                Console.WriteLine(moveFromEngine1);
                state = GameState.Error;
            }
            
            if (state != GameState.Ongoing)
            {
                result = Finishgame(state, Color.White, ref pgn);
                engine1.QuitEngine();
                engine2.QuitEngine(); 
                break;
            }
            
            pgn.PlayMove(moveFromEngine1);

            engine2.SetPosition("startpos", moves);
            Task<string> moveFromEngine2Task = Task.Run(() => engine2.GetBestMove());
            string moveFromEngine2 = moveFromEngine2Task.Result;
            
            moves += $" {moveFromEngine2} ";

            state = process_moves(moves);
            
            if (!engine2.TimeLeft())
            {
                state = GameState.TimeOut;
            }
            
            if(!IsMoveStringLegal(moveFromEngine2))
            {
                Console.WriteLine(moveFromEngine2);
                state = GameState.Error;
            }
            
            if (state != GameState.Ongoing)
            {
                result = Finishgame(state, Color.Black, ref pgn);
                engine1.QuitEngine(); 
                engine2.QuitEngine(); 
                break;
            }
            
            pgn.PlayMove(moveFromEngine2);
        }

        return result;
    }
    
    static void Main()
    {
        string engine1Path = "engine1.exe";
        string engine2Path = "engine2.exe";

        sprt = new(0.05, 0.05, 0, 5);
        openings = new();
        Database.GetRandomSample(openings, 50);
        
        ThreadPool.SetMaxThreads(12, 12);
        RunChessMatches(engine1Path, engine2Path);
        countdownEvent.Wait();
        cancelToken.Cancel();
    }
}
