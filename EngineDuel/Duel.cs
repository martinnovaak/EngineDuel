using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace EngineDuel;

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

	public void SetSPRT(double alpha, double beta, double elo0, double elo2)
	{
		sprt = new(alpha, beta, elo0, elo2);
	}

	enum Color
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

	[DllImport("chesslib.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern GameState process_moves(string moves);

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

	private void SaveResult(int result)
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

		(bool terminal, string testResult) = sprt.Test(gameResult.Wins, gameResult.Draws, gameResult.Loses);

		if (terminal)
		{
			cancelToken.Cancel();
		}

		lock (consoleLock)
		{
			string detailedStringPrint =
				$"Wins: {gameResult.Wins}, draws: {gameResult.Draws}, loses: {gameResult.Loses}. {testResult}";
			if (Duel.detailedPrint == true)
			{
				Console.WriteLine(detailedStringPrint);
			}

			if (roundCounter % 10 == 0)
			{
				if (Duel.detailedPrint == false)
				{
					Console.WriteLine(detailedStringPrint);
				}
				var wld = sprt.EloWld(wins: gameResult.Wins, losses: gameResult.Loses, draws: gameResult.Draws);

				double e1 = wld.Item1;
				double e2 = wld.Item2;
				double e3 = wld.Item3;

				Console.WriteLine($"ELO: {e2:F3} +- {(e3 - e1) / 2:F3} [{e1:F3}, {e3:F3}]");
			}
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
					ChessMatch(engine1Path, engine2Path, 1);
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
					ChessMatch(engine2Path, engine1Path, -1);
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

	private void ChessMatch(string whiteEnginePath, string blackEnginePath, int coefficient)
	{
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

		int result1 = SingleGame(whiteEnginePath, blackEnginePath, gameOpening, coefficient);
		SaveResult(coefficient * result1);

		int result2 = -SingleGame(blackEnginePath, whiteEnginePath, gameOpening, -coefficient);
		SaveResult(coefficient * result2);
	}

	int SingleGame(string whiteEnginePath, string blackEnginePath, string initialMoves, int colorCoefficient)
	{
		if (cancelToken.IsCancellationRequested)
		{
			return 0;
		}

		// Initialize communication with the engines
		UCIEngine engine1 = new UCIEngine(whiteEnginePath, initialTime, increment);
		UCIEngine engine2 = new UCIEngine(blackEnginePath, initialTime, increment);


		foreach (var option in engine1Options)
		{
			if (colorCoefficient == 1)
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
			if (colorCoefficient == 1)
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

		int numberOfMoves = 1;
		// Game loop
		while (true)
		{
			if (numberOfMoves > 150)
			{
				result = Finishgame(GameState.Draw, Color.White, ref pgn);
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
			state = process_moves(moves);

			if (!engine1.TimeLeft())
			{
				state = GameState.TimeOut;
				pgn.AddComment("Lost on time");
			}

			if (!IsMoveStringLegal(moveFromEngine1))
			{
				Console.WriteLine(moveFromEngine1);
				state = GameState.Error;
				pgn.AddComment($"Illegal move {moveFromEngine1}");
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
			string moveFromEngine2 = engine2.GetBestMove();

			moves += $" {moveFromEngine2} ";

			state = process_moves(moves);

			if (!engine2.TimeLeft())
			{
				Console.WriteLine("Lost on time");
				pgn.AddComment("Lost on time");
				state = GameState.TimeOut;
			}

			if (!IsMoveStringLegal(moveFromEngine2))
			{
				Console.WriteLine(moveFromEngine2);
				state = GameState.Error;
				pgn.AddComment($"Illegal move {moveFromEngine2}");
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

	public void Run(string engine1Path, string engine2Path, int numberOfThreads, int initTime, int incr, int rounds, List<(string, double)> options1, List<(string, double)> options2)
	{
		gameResult = new();
		cancelToken = new();
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
				// Cancel the countdown event if it's still active
				countdownEvent.Dispose();

				// Cancel the token to ensure other threads know about cancellation
				cancelToken.Cancel();
			})
			.Wait();
	}

	public (int, int, int) GetWLD()
	{
		return (gameResult.Wins, gameResult.Loses, gameResult.Draws);
	}

	public void disableDetailedPrint()
	{
		detailedPrint = false;
	}
}
