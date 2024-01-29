using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace EngineDuel;

public interface ILogger
{
	void Log(string message);
}

public class ConsoleLogger : ILogger
{
	public void Log(string message) => Console.WriteLine(message);
}

public class Duel
{
	private static CancellationTokenSource cancelToken = new();
	private static CountdownEvent countdownEvent;
	private static int roundCounter;
	private static GameResult gameResult;

	private static readonly object fileLock = new();
	private static readonly object consoleLock = new();

	private static ConcurrentStack<string> openings = new();

	private static bool detailedPrint = true;

	private SPRT sprt;
	private int initialTime;
	private int increment;
	private List<(string, double)> engine1Options;
	private List<(string, double)> engine2Options;

	private readonly ILogger logger;

	public void SetSPRT(double alpha, double beta, double elo0, double elo2) => sprt = new(alpha, beta, elo0, elo2);

	enum PlayerColor
	{
		White,
		Black
	}

	enum GameState
	{
		Ongoing,
		Draw,
		Checkmate,
		Error,
		TimeOut
	}

	[DllImport("chesslib.dll", EntryPoint = "process_moves", CallingConvention = CallingConvention.Cdecl)]
	private static extern GameState ProcessMoves(string moves);

	public static bool IsMoveStringLegal(string move)
	{
		// string has to have either 4 or 5 letters
		if (string.IsNullOrEmpty(move) || move.Length < 4 || move.Length > 5)
		{
			return false;
		}

		// Check if first and third letters are file letters (a-h)
		if (!"abcdefgh".Contains(move[0]) || !"abcdefgh".Contains(move[2]))
		{
			return false;
		}

		// Check if second and fourth letters are rank letters (1-8)
		if (!"12345678".Contains(move[1]) || !"12345678".Contains(move[3]))
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

	private void SaveResult(PlayerColor color, int result)
	{
		if (cancelToken.IsCancellationRequested)
		{
			return;
		}
		
		switch ((color, result))
		{
			case (PlayerColor.White, 1):
				gameResult.IncrementWins();
				gameResult.IncrementWhiteWins();
				break;
			case (PlayerColor.Black, 1):
				gameResult.IncrementLoses();
				gameResult.IncrementBlackLoses();
				break;
			case (PlayerColor.White, 0):
				gameResult.IncrementDraws();
				gameResult.IncrementWhiteDraws();
				break;
			case (PlayerColor.Black, 0):
				gameResult.IncrementDraws();
				gameResult.IncrementBlackDraws();
				break;
			case (PlayerColor.White , - 1):
				gameResult.IncrementLoses();	
				gameResult.IncrementWhiteLoses();
				break;
			case (PlayerColor.Black, - 1):
				gameResult.IncrementWins();
				gameResult.IncrementBlackWins();
				break;
		}

		(bool testFinished, string testResult) = sprt.Test(gameResult.Wins, gameResult.Draws, gameResult.Loses);

		lock (consoleLock)
		{
			string detailedStringPrint =
				$"Wins: {gameResult.Wins}, draws: {gameResult.Draws}, loses: {gameResult.Loses}. {testResult}";
			if (Duel.detailedPrint == true)
			{
				logger.Log(detailedStringPrint);
			}

			if (roundCounter % 10 == 0)
			{
				if (Duel.detailedPrint == false)
				{
					logger.Log(detailedStringPrint);
				}
				
				(double e1, double e2, double e3) = sprt.EloWld(wins: gameResult.Wins, losses: gameResult.Loses, draws: gameResult.Draws);

				logger.Log($"ELO: {e2:F3} +- {(e3 - e1) / 2:F3} [{e1:F3}, {e3:F3}]");
			}
		}

		if (testFinished)
		{
			cancelToken.Cancel();
		}
	}

	static int Finishgame(GameState state, PlayerColor color, ref PGN pgn)
	{
		string result = (state, color) switch
		{
			(GameState.Draw, _) => "1/2-1/2",
			(GameState.Checkmate, PlayerColor.White) => "1-0",
			(GameState.Checkmate, PlayerColor.Black) => "0-1",
			(GameState.Error, PlayerColor.White) => "0-1",
			(GameState.Error, PlayerColor.Black) => "1-0",
			(GameState.TimeOut, PlayerColor.White) => "0-1",
			(GameState.TimeOut, PlayerColor.Black) => "1-0",
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

	private void RunChessMatches(string engine1Path, string engine2Path, int rounds, int numberOfThreads)
	{
		Semaphore semaphore = new Semaphore(numberOfThreads, numberOfThreads);
		for (int i = 0; i < rounds; i++)
		{
			semaphore.WaitOne();
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					ChessMatch(engine1Path, engine2Path, PlayerColor.White);
				}
				finally
				{
					if (!cancelToken.IsCancellationRequested && countdownEvent.CurrentCount > 0)
					{
						countdownEvent.Signal();
					}
					semaphore.Release();
				}
			}, cancelToken.Token);

			semaphore.WaitOne();
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					ChessMatch(engine2Path, engine1Path, PlayerColor.Black);
				}
				finally
				{
					if (!cancelToken.IsCancellationRequested && countdownEvent.CurrentCount > 0)
					{
						countdownEvent.Signal();
					}
					semaphore.Release();
				}
			}, cancelToken.Token);
		}
	}

	private void ChessMatch(string whiteEnginePath, string blackEnginePath, PlayerColor color)
	{
		if (cancelToken.IsCancellationRequested)
		{
			return;
		}

		const int minimumOpeningsCount = 10;
		const int openingsToRetrieve = 20;

		if (openings.Count < minimumOpeningsCount)
		{
			Database.GetRandomSample(openings, openingsToRetrieve);
		}

		string gameOpening;
		if (!openings.TryPop(out gameOpening))
		{
			gameOpening = "";
		}

		int result1 = SingleGame(whiteEnginePath, blackEnginePath, gameOpening, color);
		SaveResult(color, result1);

		PlayerColor changedColor = color == PlayerColor.White ? PlayerColor.Black : PlayerColor.White;
		int result2 = -SingleGame(blackEnginePath, whiteEnginePath, gameOpening, changedColor);
		SaveResult(color, result2);
	}

	int SingleGame(string whiteEnginePath, string blackEnginePath, string initialMoves, PlayerColor color)
	{
		if (cancelToken.IsCancellationRequested)
		{
			return 0;
		}

		// Initialize communication with the engines
		UCIEngine engine1 = new UCIEngine(whiteEnginePath, initialTime, increment, logger);
		UCIEngine engine2 = new UCIEngine(blackEnginePath, initialTime, increment, logger);


		foreach (var option in engine1Options)
		{
			if (color == PlayerColor.White)
			{
				engine1.SetOption(option.Item1, option.Item2);
			}
			else
			{
				engine2.SetOption(option.Item1, option.Item2);
			}
		}

		foreach (var option in engine2Options)
		{
			if (color == PlayerColor.White)
			{
				engine2.SetOption(option.Item1, option.Item2);
			}
			else
			{
				engine1.SetOption(option.Item1, option.Item2);
			}
		}

		string moves = initialMoves;
		GameState state;
		int result = 0;

		PGN pgn = new(engine1.GetName(), engine2.GetName());
		foreach (string move in moves.Split(" ", StringSplitOptions.RemoveEmptyEntries))
		{
			pgn.PlayMove(move);
		}

		// Game loop
		int numberOfMoves = 1;
		while (true)
		{
			if (numberOfMoves > 250)
			{
				result = Finishgame(GameState.Draw, PlayerColor.White, ref pgn);
				engine1.QuitEngine();
				engine2.QuitEngine();
				break;
			}

			if (cancelToken.IsCancellationRequested)
			{
				engine1.QuitEngine();
				engine2.QuitEngine();
				break;
			}

			engine1.SetPosition("startpos", moves);
			string moveFromEngine1 = engine1.GetBestMove();

			moves += $" {moveFromEngine1} ";
			state = ProcessMoves(moves);

			if (!engine1.HasTimeLeft())
			{
				state = GameState.TimeOut;
				pgn.AddComment("Lost on time");
			}

			if (!IsMoveStringLegal(moveFromEngine1))
			{
				state = GameState.Error;
				pgn.AddComment($"Illegal move {moveFromEngine1}");
			}

			if (state != GameState.Ongoing)
			{
				result = Finishgame(state, PlayerColor.White, ref pgn);
				engine1.QuitEngine();
				engine2.QuitEngine();
				break;
			}

			pgn.PlayMove(moveFromEngine1);

			engine2.SetPosition("startpos", moves);
			string moveFromEngine2 = engine2.GetBestMove();

			moves += $" {moveFromEngine2} ";

			state = ProcessMoves(moves);

			if (!engine2.HasTimeLeft())
			{
				pgn.AddComment("Lost on time");
				state = GameState.TimeOut;
			}

			if (!IsMoveStringLegal(moveFromEngine2))
			{
				pgn.AddComment($"Illegal move {moveFromEngine2}");
				state = GameState.Error;
			}

			if (state != GameState.Ongoing)
			{
				result = Finishgame(state, PlayerColor.Black, ref pgn);
				engine1.QuitEngine();
				engine2.QuitEngine();
				break;
			}

			pgn.PlayMove(moveFromEngine2);
			numberOfMoves++;
		}

		return result;
	}

	public async Task Run(string engine1Path, string engine2Path, int numberOfThreads, int initTime, int incr, int rounds, List<(string, double)> options1, List<(string, double)> options2, CancellationTokenSource guiCancellationToken)
	{
		gameResult = new();
		cancelToken = guiCancellationToken;
		countdownEvent = new(2 * rounds);

		initialTime = initTime;
		increment = incr;
		engine1Options = options1;
		engine2Options = options2;

		Database.GetRandomSample(openings, 50);

		RunChessMatches(engine1Path, engine2Path, rounds, numberOfThreads);

		Task.WhenAny(Task.Run(() => countdownEvent.Wait()), Task.Delay(Timeout.Infinite, cancelToken.Token))
			.ContinueWith(_ => 
			{
				countdownEvent.Dispose(); // Cancel the countdown event if it's still active
				
				cancelToken.Cancel(); // Cancel the token to ensure other threads know about cancellation
			})
			.Wait();
		
		(double e1, double e2, double e3) = sprt.EloWld(wins: gameResult.Wins, losses: gameResult.Loses, draws: gameResult.Draws);
		string finalPrint = $"________________________________________________\n End of match: " +
			$"Wins: {gameResult.Wins}, draws: {gameResult.Draws}, loses: {gameResult.Loses}.\n" +
			$"Wins as white: {gameResult.WhiteWins}, draws as white: {gameResult.WhiteDraws}, loses as white: {gameResult.WhiteLoses}\n" +
			$"Wins as black: {gameResult.BlackWins}, draws as black: {gameResult.BlackDraws}, loses as black: {gameResult.BlackLoses}\n" +
			$"ELO: {e2:F3} +- {(e3 - e1) / 2:F3} [{e1:F3}, {e3:F3}]";
		logger.Log(finalPrint);
	}

	public (int, int, int) GetWLD() => (gameResult.Wins, gameResult.Loses, gameResult.Draws);

	public void DisableDetailedPrint() => detailedPrint = false;

	public Duel(ILogger logger = null) => this.logger = logger ?? new ConsoleLogger();
}
