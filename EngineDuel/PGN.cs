namespace engine_test;

public class PGN
{
    private string whiteEngine;
    private string blackEngine;
    private List<string> moves;

    public PGN(string white, string black)
    {
        whiteEngine = white;
        blackEngine = black;
        moves = new();
    }
    
    private char[] ChessBoard =
    {
        'R', 'N', 'B', 'Q', 'K', 'B', 'N', 'R',
        'P', 'P', 'P', 'P', 'P', 'P', 'P', 'P',
        ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ',
        ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ',
        ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ',
        ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ',
        'p', 'p', 'p', 'p', 'p', 'p', 'p', 'p',
        'r', 'n', 'b', 'q', 'k', 'b', 'n', 'r'
    };

    public enum Square : int
    {
        A1, B1, C1, D1, E1, F1, G1, H1,
        A2, B2, C2, D2, E2, F2, G2, H2,
        A3, B3, C3, D3, E3, F3, G3, H3,
        A4, B4, C4, D4, E4, F4, G4, H4,
        A5, B5, C5, D5, E5, F5, G5, H5,
        A6, B6, C6, D6, E6, F6, G6, H6,
        A7, B7, C7, D7, E7, F7, G7, H7,
        A8, B8, C8, D8, E8, F8, G8, H8
    }

    private static readonly Dictionary<string, Square> SquareMap = new()
    {
        {"a1", Square.A1}, {"a2", Square.A2}, {"a3", Square.A3}, {"a4", Square.A4},
        {"a5", Square.A5}, {"a6", Square.A6}, {"a7", Square.A7}, {"a8", Square.A8},
        {"b1", Square.B1}, {"b2", Square.B2}, {"b3", Square.B3}, {"b4", Square.B4},
        {"b5", Square.B5}, {"b6", Square.B6}, {"b7", Square.B7}, {"b8", Square.B8},
        {"c1", Square.C1}, {"c2", Square.C2}, {"c3", Square.C3}, {"c4", Square.C4},
        {"c5", Square.C5}, {"c6", Square.C6}, {"c7", Square.C7}, {"c8", Square.C8},
        {"d1", Square.D1}, {"d2", Square.D2}, {"d3", Square.D3}, {"d4", Square.D4},
        {"d5", Square.D5}, {"d6", Square.D6}, {"d7", Square.D7}, {"d8", Square.D8},
        {"e1", Square.E1}, {"e2", Square.E2}, {"e3", Square.E3}, {"e4", Square.E4},
        {"e5", Square.E5}, {"e6", Square.E6}, {"e7", Square.E7}, {"e8", Square.E8},
        {"f1", Square.F1}, {"f2", Square.F2}, {"f3", Square.F3}, {"f4", Square.F4},
        {"f5", Square.F5}, {"f6", Square.F6}, {"f7", Square.F7}, {"f8", Square.F8},
        {"g1", Square.G1}, {"g2", Square.G2}, {"g3", Square.G3}, {"g4", Square.G4},
        {"g5", Square.G5}, {"g6", Square.G6}, {"g7", Square.G7}, {"g8", Square.G8},
        {"h1", Square.H1}, {"h2", Square.H2}, {"h3", Square.H3}, {"h4", Square.H4},
        {"h5", Square.H5}, {"h6", Square.H6}, {"h7", Square.H7}, {"h8", Square.H8},
    };

    public void PlayMove(string uciMove)
    {
        string[] squares = uciMove
            .Select((c, i) => new { Character = c, Index = i })
            .GroupBy(ch => ch.Index / 2, ch => ch.Character)
            .Select(g => new string(g.ToArray()))
            .ToArray();

        string from = squares[0];
        string to = squares[1];
        string promotion = squares.Length > 2 ? squares[2] : "";

        Square squareFrom = SquareMap[from];
        Square squareTo = SquareMap[to];
        char pieceChar = ChessBoard[(int)squareFrom];

        ChessBoard[(int)squareFrom] = ' ';
        ChessBoard[(int)squareTo] = pieceChar;

        string pgnMove = from + to;

        if (pieceChar != 'P' && pieceChar != 'p')
        {
            pgnMove = char.ToUpper(pieceChar) + pgnMove;
        }

        if (!string.IsNullOrEmpty(promotion))
        {
            pgnMove += "=" + promotion;
        }

        if (pieceChar == 'K' && squareFrom == Square.E1)
        {
            if (squareTo == Square.C1)
            {
                ChessBoard[(int)Square.A1] = ' ';
                ChessBoard[(int)Square.D1] = 'R';
                pgnMove = "O-O-0";
            }
            else if (squareTo == Square.G1)
            {
                ChessBoard[(int)Square.H1] = ' ';
                ChessBoard[(int)Square.F1] = 'R';
                pgnMove = "O-O";
            }
        }
        else if (pieceChar == 'k' && squareFrom == Square.E8)
        {
            if (squareTo == Square.C8)
            {
                ChessBoard[(int)Square.A8] = ' ';
                ChessBoard[(int)Square.D8] = 'r';
                pgnMove = "O-O-0";
            }
            else if (squareTo == Square.G8)
            {
                ChessBoard[(int)Square.H8] = ' ';
                ChessBoard[(int)Square.F8] = 'r';
                pgnMove = "O-O";
            }
        }

        moves.Add(pgnMove);
    }

    public string GetGame(string result, int currentRound)
    {
        DateTime currentDate = DateTime.Now;
        string formattedDate = currentDate.ToString("yyyy.MM.dd");
        string pgnMoves = "";
        for (int i = 0; i < moves.Count; i++)
        {
            if (i % 2 == 0)
            {
                pgnMoves += $"{i + 1}. ";
            }

            pgnMoves += moves[i] + " ";
        }

        pgnMoves += result;

        return $"[Event \"?\"] \n" +
               $"[Site \"?\"] \n" +
               $"[Date \"{formattedDate}\"] \n" +
               $"[Round \"{currentRound}\"] \n" +
               $"[White \"{whiteEngine}\"] \n" +
               $"[Black \"{blackEngine}\"] \n" +
               $"[Result \"{result}\"] \n" +
               $"{pgnMoves} \n\n";
    }
}