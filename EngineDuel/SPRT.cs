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
        double a0 = 2.50662823884;
        double a1 = -18.61500062529;
        double a2 = 41.39119773534;
        double a3 = -25.44106049637;
        double b1 = -8.47351093090;
        double b2 = 23.08336743743;
        double b3 = -21.06224101826;
        double b4 = 3.13082909833;
        double c0 = -2.78718931138;
        double c1 = -2.29796479134;
        double c2 = 4.85014127135;
        double c3 = 2.32121276858;
        double d1 = 3.54388924762;
        double d2 = 1.63706781897;

        double x = p - 0.5;
        double result;

        if (Math.Abs(x) < 0.42)
        {
            double y = x * x;
            result = x * (((a3 * y + a2) * y + a1) * y + a0) /
                     ((((b4 * y + b3) * y + b2) * y + b1) * y + 1);
        }
        else
        {
            double y = Math.Sqrt(-Math.Log(Math.Min(p, 1 - p)));
            result = (((c3 * y + c2) * y + c1) * y + c0) /
                     ((d2 * y + d1) * y + 1);
            if (x < 0)
            {
                result = -result;
            }
        }

        return result;
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
