using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EngineDuel;

public struct GameResult
{
	private int wins;
	private int draws;
	private int loses;

	private int whiteWins;
	private int blackWins;

	private int whiteDraws;
	private int blackDraws;

	private int whiteLoses;
	private int blackLoses;

	public int Wins => wins;
	public int Draws => draws;
	public int Loses => loses;

	public int WhiteWins => whiteWins;
	public int BlackWins => blackWins;

	public int WhiteDraws => whiteDraws;
	public int BlackDraws => blackDraws;

	public int WhiteLoses => whiteLoses;
	public int BlackLoses => blackLoses;

	public void IncrementWins() => Interlocked.Increment(ref wins);
	public void IncrementDraws() => Interlocked.Increment(ref draws);
	public void IncrementLoses() => Interlocked.Increment(ref loses);
	public void IncrementWhiteWins() => Interlocked.Increment(ref whiteWins);

	public void IncrementBlackWins() => Interlocked.Increment(ref blackWins);

	public void IncrementWhiteDraws() => Interlocked.Increment(ref whiteDraws);

	public void IncrementBlackDraws() => Interlocked.Increment(ref blackDraws);

	public void IncrementWhiteLoses() => Interlocked.Increment(ref whiteLoses);

	public void IncrementBlackLoses() => Interlocked.Increment(ref blackLoses);
}
