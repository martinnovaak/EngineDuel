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

    // Abramowitz and Stegun
    public double PhiInv(double p)
    {
        double t = Math.Sqrt(-2.0 * Math.Log(Math.Min(p, 1 - p)));
        double a = 0.010328, b = 0.802853, c = 2.515517;
        double d = 0.001308, e = 0.189269, f = 1.432788;
        
        t -= ((a * t + b) * t + c) / (((d * t + e) * t + f) * t + 1.0);

        return p >= 0.5 ? t : -t;
    }

    public double Elo(double score)
    {
        if (score <= 0 || score >= 1)
        {
            return 0.0;
        }

        return -400 * Math.Log10(1 / score - 1);
    }

    public Tuple<double, double, double> EloWld(int wins, int losses, int draws)
    {
        int N = wins + losses + draws;
        if (N == 0)
        {
            return Tuple.Create(0.0, 0.0, 0.0);
        }

        double p_w = (double)wins / N;
        double p_l = (double)losses / N;
        double p_d = (double)draws / N;

        double mu = p_w + p_d / 2;
        double stdev = Math.Sqrt(p_w * Math.Pow(1 - mu, 2) + p_l * Math.Pow(0 - mu, 2) + p_d * Math.Pow(0.5 - mu, 2)) / Math.Sqrt(N);

        // 95% confidence interval for mu
        double mu_min = mu + PhiInv(0.025) * stdev;
        double mu_max = mu + PhiInv(0.975) * stdev;

        return Tuple.Create(Elo(mu_min), Elo(mu), Elo(mu_max));
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

    public (bool, string) Test(int wins, int draws, int loses)
    {
        if ((wins == 0 && draws == 0) || (wins == 0 && loses == 0) || (draws == 0 && loses == 0))
        {
            return (false, "Keep playing");
        }
        
        double llr = gsprt(wins, draws, loses);

        bool terminal = false;
        string message = $"{lower:F4} < {llr:F4} < {upper:F4}, keep playing";

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
