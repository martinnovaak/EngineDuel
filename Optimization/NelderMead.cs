namespace EngineDuel;

public class Optimizer
{
    private string enginePath;
    private int numberOfThreads;
    private int numberOfRounds;
    private int gameTime;
    private int gameIncrement;
    private List<(string, double)> engineOptions;
    
    public Tuple<double[], double> NelderMead(Func<double[], double> f, double[] xStart,
        double step = 0.5, double noImproveThr = 1e-6,
        int noImprovBreak = 10, int maxIter = 0,
        double alpha = 1.0, 
        double gamma = 2.0, 
        double rho = 0.75, 
        double sigma = 0.5)
    {
        int dim = xStart.Length;
        int eval = 0;

        gamma += 2.0 / dim;
        rho -= 1.0 / (2.0 * dim);
        sigma -= 1.0 / dim;
        
        //double prevBest = f(xStart);
        double prevBest = 0.5;
        int noImprov = 0;
        var res = new List<Tuple<double[], double>> { Tuple.Create(xStart, prevBest) };

        for (int i = 0; i < dim; i++)
        {
            var x = (double[])xStart.Clone();
            x[i] += step;
            printPoint("Testing intial point", x);
            double score = f(x);
            eval++;
            res.Add(Tuple.Create(x, score));
        }

        int iters = 0;
        while (true)
        {
            // Order the points based on their function values
            res = res.OrderBy(t => t.Item2).ToList();
            double best = res[0].Item2;
            
            Console.WriteLine(best);

            // Check termination conditions
            if (maxIter > 0 && iters >= maxIter)
            {
                return Tuple.Create(res[0].Item1, res[0].Item2);
            }

            iters++;

            if (best < prevBest - noImproveThr)
            {
                noImprov = 0;
                prevBest = best;
            }
            else
            {
                noImprov++;
            }

            if (noImprov >= noImprovBreak)
            {
                return Tuple.Create(res[0].Item1, res[0].Item2);
            }
            
            Console.WriteLine($"{iters}: {best}, number of evals: {eval}");
            printPoint("Best point", res[0].Item1);

            // Calculate the centroid of the simplex (excluding the worst point)
            double[] centroid = res.Take(res.Count - 1)
                .Aggregate(new double[dim], (acc, tup) => acc.Zip(tup.Item1, (sum, x) => sum + x / (res.Count - 1)).ToArray());

            // Reflection
            double[] xr = centroid.Select((xi, i) => xi + alpha * (xi - res.Last().Item1[i])).ToArray();
            printPoint("Testing refleced point", xr);
            double rscore = f(xr);
            eval++;

            if (res[0].Item2 <= rscore && rscore < res[^2].Item2)
            {
                Console.WriteLine("reflected");
                res[^1] = Tuple.Create(xr, rscore);  // Replace the last point with the reflected point
                continue;
            }

            // Expansion
            if (rscore < res[0].Item2)
            {
                // expansion point
                double[] xe = centroid.Select((xi, i) => xi + gamma * (xr[i] - xi)).ToArray();
                printPoint("Testing expanded point", xe);
                double escore = f(xe);
                eval++;
                
                // Replace the last point with the expanded point / reflected point
                res[^1] = escore < rscore ? Tuple.Create(xe, escore) : Tuple.Create(xr, rscore);
                Console.WriteLine("expanded");
                continue;
            }

            // Contraction
            if (rscore < res[^1].Item2)
            {
                double[] xoc = centroid.Select((xi, i) => xi + rho * (xr[i] - xi)).ToArray();
                printPoint("Testing outside contract point", xoc);
                double cscore = f(xoc);
                eval++;

                if (cscore < rscore)
                {
                    res[^1] = Tuple.Create(xoc, cscore);  // Replace the last point with the contracted point
                    Console.WriteLine("contracted outside");
                    continue;
                }
            }
            else
            {
                double[] xic = centroid.Select((xi, i) => xi - rho * (xr[i] - xi)).ToArray();
                printPoint("Testing inner contract point", xic);
                double fic = f(xic);
                eval++;

                if (fic < res[^1].Item2)
                {
                    Console.WriteLine("contracted inside");
                    res[^1] = Tuple.Create(xic, fic);  // Replace the last point with the inside contracted point
                    continue;
                }
            }

            // Shrink
            Console.WriteLine("shrinked");
            double[] x1 = res[0].Item1;
            res = res
                .Skip(1)
                .Select(tup =>
                {
                    double[] shrinkedX = x1.Zip(tup.Item1, (xi, tupi) => xi + sigma * (tupi - xi)).ToArray();
                    printPoint("Testing shrink point", shrinkedX);
                    double score = f(shrinkedX);
                    eval++;
                    return Tuple.Create(shrinkedX, score);
                })
                .Prepend(res[0])  // Add the unchanged best point back to the beginning
                .ToList();
        }
    }

    public double GetScore(double[] point)
    {
        List<(string, double)> options = new();
        if (point.Length != engineOptions.Count)
        {
            throw new ArgumentException("Point dimensions do not match engineOptions dimensions.");
        }

        for (int i = 0; i < point.Length; i++)
        {
            options.Add((engineOptions[i].Item1, point[i]));
        }
        Duel duel = new();
        duel.SetSPRT(0.05, 0.05, 0, 5);
        duel.disableDetailedPrint();
        duel.Run(enginePath, enginePath, numberOfThreads, gameTime, gameIncrement, numberOfRounds, options);

        (int wins, int loses, int draws)  = duel.GetWLD();

        int N = wins + loses + draws;

        return (loses + draws * 0.5) / (double)N;
    }

    void printPoint(string message, double[] point)
    {
        Console.Write($"{message}: [");
        for (int i = 0; i < point.Length; i++)
        {
            Console.Write($"{point[i]:F3}");
            if (i < point.Length - 1)
            {
                Console.Write(", ");
            }
        }
        Console.WriteLine("]");
    }

    public Optimizer(string path, int threads, int rounds, int time, int increment, List<(string, double)> options)
    {
        enginePath = path;
        numberOfThreads = threads;
        numberOfRounds = rounds;
        gameTime = time;
        gameIncrement = increment;
        engineOptions = options;
    }

    public void Optimize()
    {
        double[] start = engineOptions.Select(item => item.Item2).ToArray();
        var result = NelderMead(GetScore, start);

        double[] point = result.Item1;
        double value = result.Item2;
        
        printPoint("Best point", point);
        // Print the double value
        Console.WriteLine($"Value: {value:F3}");
    }
}
