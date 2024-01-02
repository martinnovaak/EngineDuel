using System.Drawing;

namespace EngineDuel;

public class Optimizer
{
	private string enginePath1;
	private string enginePath2;
	private int numberOfThreads;
	private int numberOfRounds;
	private int gameTime;
	private int gameIncrement;
	private List<(string, double, double)> engineOptions;
	private ILogger logger;

	public int GetScore(double[] perturbedThetaPositive, double[] perturbedThetaNegative)
	{
		List<(string, double)> optionsEngine1 = new();
		List<(string, double)> optionsEngine2 = new();

		for (int i = 0; i < perturbedThetaPositive.Length; i++)
		{
			Console.WriteLine($"{perturbedThetaPositive[i]} {perturbedThetaNegative[i]}");
			optionsEngine1.Add((engineOptions[i].Item1, perturbedThetaPositive[i]));
			optionsEngine2.Add((engineOptions[i].Item1, perturbedThetaNegative[i]));
		}

		Duel duel = new();
		duel.SetSPRT(0.01, 0.01, -5, 5);
		duel.disableDetailedPrint();
		duel.Run(enginePath1, enginePath2, numberOfThreads, gameTime, gameIncrement, numberOfRounds, optionsEngine1, optionsEngine2);

		(int wins, int loses, int draws) = duel.GetWLD();

		return wins - loses;
	}

	public double[] SpsaGradientEstimate(double[] theta, double[] deltaK)
	{
		double[] perturbedThetaPositive = theta.Zip(deltaK, (t, dk) => t + dk).ToArray();
		double[] perturbedThetaNegative = theta.Zip(deltaK, (t, dk) => t - dk).ToArray();

		printPoints("Testing: ", perturbedThetaPositive, perturbedThetaNegative);

		// double functionPos = objectiveFunction(perturbedThetaPos);
		// double functionNeg = objectiveFunction(perturbedThetaNeg);

		int score = -GetScore(perturbedThetaPositive, perturbedThetaNegative);

		return theta.Zip(deltaK, (t, dk) => score / (2 * dk)).ToArray();
	}

	public double[] AdamWithSpsa(double[] initialTheta, int maxIterations, double beta1 = 0.9, double beta2 = 0.999, double epsilon = 1e-8)
	{
		double[] theta = (double[])initialTheta.Clone();
		double[] m = new double[theta.Length];
		double[] v = new double[theta.Length];
		double[] a = engineOptions.Select(option => option.Item3).ToArray();
		double[] c = engineOptions.Select(option => option.Item3).ToArray();
		
		int t = 1;
		Random random = new Random(Guid.NewGuid().GetHashCode());

		for (int iteration = 0; iteration < maxIterations; iteration++)
		{
			double[] aK = a.Select((value, index) => value / Math.Pow(t + maxIterations / 10, 0.601)).ToArray(); 
			double[] cK = c.Select((value, index) => value / Math.Pow(t, 0.102)).ToArray();

			double[] deltaK = cK.Select(ck => ck * (random.Next(2) * 2 - 1)).ToArray();

			double[] gradientEstimate = SpsaGradientEstimate(theta, deltaK);

			m = m.Zip(gradientEstimate, (mi, gi) => beta1 * mi + (1 - beta1) * gi).ToArray();
			v = v.Zip(gradientEstimate, (vi, gi) => beta2 * vi + (1 - beta2) * gi * gi).ToArray();

			double[] m_ = m.Select(mi => mi / (1 - Math.Pow(beta1, t))).ToArray();
			double[] v_ = v.Select(vi => vi / (1 - Math.Pow(beta2, t))).ToArray();

			theta = theta.Select((ti, i) => ti - aK[i] * m_[i] / (Math.Sqrt(v_[i]) + epsilon)).ToArray();

			t++;

			printPoint($"Iteration number: {t}, Best values: ", theta);
		}

		return theta;
	}

	public double[] SgdWithSpsa(double[] initialTheta, int maxIterations, double beta1 = 0.9, double beta2 = 0.999, double epsilon = 1e-8)
	{
		double[] theta = (double[])initialTheta.Clone();
		double[] a = engineOptions.Select(option => option.Item3).ToArray();
		double[] c = engineOptions.Select(option => option.Item3).ToArray();
		int t = 1;
		Random random = new Random(Guid.NewGuid().GetHashCode());

		for (int iteration = 0; iteration < maxIterations; iteration++)
		{
			double[] aK = a.Select((value, index) => value / Math.Pow((t - 1) * 4 * numberOfRounds + maxIterations / 10, 0.601)).ToArray();
			double[] cK = c.Select((value, index) => value / Math.Pow((t - 1) * 4 * numberOfRounds + 1, 0.102)).ToArray();

			double[] deltaK = cK.Select(ck => ck * (random.Next(2) * 2 - 1)).ToArray();

			double[] gradientEstimate = SpsaGradientEstimate(theta, deltaK);

			theta = theta.Select((ti, i) => ti - aK[i] * gradientEstimate[i]).ToArray();

			t++;

			printPoint($"Iteration number: {t}, Best values: ", theta);
		}

		return theta;
	}

	void printPoints(string message, double[] point1, double[] point2)
	{
		logger.Log($"{message}: [{string.Join(", ", point1)}] vs [{string.Join(", ", point2)}]");
	}

	void printPoint(string message, double[] point)
	{
		logger.Log($"{message}: [{string.Join(", ", point)}]");
	}

	public Optimizer(string enginePath1, string enginePath2, int numberOfThreads, int numberOfRounds, int gameTime, int gameIncrement, List<(string, double, double)> engineOptions, ILogger logger = null)
	{
		this.enginePath1 = enginePath1;
		this.numberOfThreads = numberOfThreads;
		this.numberOfRounds = numberOfRounds;
		this.gameTime = gameTime;
		this.gameIncrement = gameIncrement;
		this.engineOptions = engineOptions;
		this.logger = logger ?? new ConsoleLogger(); // Use the provided logger or default to ConsoleLogger
	}

	public void Optimize()
	{
		double[] start = engineOptions.Select(item => item.Item2).ToArray();
		var result = AdamWithSpsa(start, 100);

		//double[] point = result.Item1;
		//double value = result.Item2;

		//printPoint("Best point", point);
		// Print the double value
		//Console.WriteLine($"Value: {value:F3}");
	}
}
