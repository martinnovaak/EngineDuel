namespace EngineDuel;

public class SPRT
{
    private double lower;
    private double upper;
    private double p0;
    private double p1;

    public SPRT(double alpha, double beta, double elo0, double elo1)
    {
        lower = double.Log(beta / (1.0 - alpha));
        upper = double.Log((1.0 - beta) / alpha);
        p0 = 1.0 / (1.0 + Math.Pow(10, -elo0 / 400.0));
        p1 = 1.0 / (1.0 + Math.Pow(10, -elo1 / 400.0));
    }

    public double gsprt(int wins, int draws, int loses)
    {
        int N = wins + draws + loses;

        if (N == 0) return 0.0;
        
        double w = (double)wins / N;
        double d = (double)draws / N;

        double X = w + d / 2;
        double variance = (w + d / 4 - X * X) / N;

        return (p1 - p0) * (2 * X - p0 - p1) / (2 * variance);
    }

    public (bool, string) test(int wins, int draws, int loses)
    {
        if ((wins == 0 && draws == 0) || (wins == 0 && loses == 0) || (draws == 0 && loses == 0))
        {
            return (false, "Keep playing");
        }
        
        double llr = gsprt(wins, draws, loses);

        bool terminal = false;
        string message = "Keep playing";

        if (llr > upper)
        {
            terminal = true;
            message = "H1 accepted";
        } 
        else if (llr <= lower)
        {
            terminal = true;
            message = "H0 accepted";
        }

        return (terminal, message);
    }
}