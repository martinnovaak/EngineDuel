using System.Drawing;

namespace EngineDuel;

public class Optimizer
{
	private string enginePath;
	private int numberOfThreads;
	private int numberOfRounds;
	private int gameTime;
	private int gameIncrement;
	private List<(string, double, double)> engineOptions;

	public int GetScore(double[] perturbedThetaPositive, double[] perturbedThetaNegative)
	{
		List<(string, double)> optionsEngine1 = new();
		List<(string, double)> optionsEngine2 = new();

		for (int i = 0; i < perturbedThetaPositive.Length; i++)
		{
			optionsEngine1.Add((engineOptions[i].Item1, perturbedThetaPositive[i]));
			optionsEngine2.Add((engineOptions[i].Item1, perturbedThetaNegative[i]));
		}

			Duel duel = new();
		duel.SetSPRT(0.01, 0.01, -5, 5);
		duel.disableDetailedPrint();
		duel.Run(enginePath, enginePath, numberOfThreads, gameTime, gameIncrement, numberOfRounds, optionsEngine1, optionsEngine2);

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
		double[] c = engineOptions.Select(option => option.Item3).ToArray();
		int t = 1;
		Random random = new Random(Guid.NewGuid().GetHashCode());

		for (int iteration = 0; iteration < maxIterations; iteration++)
		{
			double aK = 1.0 / Math.Pow((t - 1) * numberOfRounds + maxIterations / 10, 0.601);
			double[] cK = c.Select((value, index) => value / Math.Pow((t - 1) * numberOfRounds + 1, 0.102)).ToArray();

			double[] deltaK = cK.Select(ck => ck * (random.Next(2) * 2 - 1)).ToArray();

			double[] gradientEstimate = SpsaGradientEstimate(theta, deltaK);

			m = m.Zip(gradientEstimate, (mi, gi) => beta1 * mi + (1 - beta1) * gi).ToArray();
			v = v.Zip(gradientEstimate, (vi, gi) => beta2 * vi + (1 - beta2) * gi * gi).ToArray();

			double[] m_ = m.Select(mi => mi / (1 - Math.Pow(beta1, t))).ToArray();
			double[] v_ = v.Select(vi => vi / (1 - Math.Pow(beta2, t))).ToArray();

			theta = theta.Zip(m, (ti, mi) => ti - aK * mi / (Math.Sqrt(v.First()) + epsilon)).ToArray();

			t++;

			printPoint($"Iteration number: {t}, Best values: ", theta);
		}

		return theta;
	}

	public double[] SgdWithSpsa(double[] initialTheta, int maxIterations, double beta1 = 0.9, double beta2 = 0.999, double epsilon = 1e-8)
	{
		double[] theta = (double[])initialTheta.Clone();
		int t = 1;
		Random random = new Random(Guid.NewGuid().GetHashCode());

		for (int iteration = 0; iteration < maxIterations; iteration++)
		{
			double aK = 1.0 / Math.Pow(t + maxIterations / 10, 0.601);
			double cK = 1.0 / Math.Pow(t, 0.102);

			double[] deltaK = Enumerable.Range(0, theta.Length).Select(_ => cK * (random.Next(2) * 2 - 1)).ToArray();

			double[] gradientEstimate = SpsaGradientEstimate(theta, deltaK);

			theta = theta.Zip(gradientEstimate, (ti, gi) => ti - aK * gi).ToArray();

			t++;

			printPoint($"Iteration number: {t}, Best values: ", theta);
		}

		return theta;
	}

	void printPoints(string message, double[] point1, double[] point2)
	{
		Console.Write($"{message}: [");
		for (int i = 0; i < point1.Length; i++)
		{
			Console.Write($"{point1[i]:F3}");
			if (i < point1.Length - 1)
			{
				Console.Write(", ");
			}
		}
		Console.Write("] vs [");
		for (int i = 0; i < point2.Length; i++)
		{
			Console.Write($"{point2[i]:F3}");
			if (i < point2.Length - 1)
			{
				Console.Write(", ");
			}
		}
		Console.WriteLine("]");
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

	public Optimizer(string enginePath, int numberOfThreads, int numberOfRounds, int gameTime, int gameIncrement, List<(string, double, double)> engineOptions)
	{
		this.enginePath = enginePath;
		this.numberOfThreads = numberOfThreads;
		this.numberOfRounds = numberOfRounds;
		this.gameTime = gameTime;
		this.gameIncrement = gameIncrement;
		this.engineOptions = engineOptions;
	}

	public void Optimize()
	{
		double[] start = engineOptions.Select(item => item.Item2).ToArray();
		var result = AdamWithSpsa(start, 10000);

		//double[] point = result.Item1;
		//double value = result.Item2;

		//printPoint("Best point", point);
		// Print the double value
		//Console.WriteLine($"Value: {value:F3}");
	}
}
