using System.Globalization;
using EngineDuel;
using CommandLine;

class Options
{
	private const string timeControlHelpText = "Time control for the chess match." +
											   " The format should be 'initialTime+increment', where " +
											   "both 'initialTime' and 'increment' are specified in seconds. " +
											   "Example: '60+1' for 1 minute initial time with a 1-second increment.";

	[Option("engine1", Required = true, HelpText = "Path to the first chess engine.")]
	public string Engine1Path { get; set; }

	[Option("engine2", Required = true, HelpText = "Path to the second chess engine.")]
	public string Engine2Path { get; set; }

	[Option("alpha", Required = false, Default = 0.05, HelpText = "Alpha value for SPRT.")]
	public double Alpha { get; set; }

	[Option("beta", Required = false, Default = 0.05, HelpText = "Beta value for SPRT.")]
	public double Beta { get; set; }

	[Option("elo0", Required = false, Default = 0, HelpText = "Elo0 value for SPRT.")]
	public double Elo0 { get; set; }

	[Option("elo1", Required = false, Default = 5, HelpText = "Elo1 value for SPRT.")]
	public double Elo1 { get; set; }

	[Option("threads", Required = false, Default = 1, HelpText = "Number of threads.")]
	public int NumberOfThreads { get; set; }

	[Option("rounds", Required = false, Default = 100, HelpText = "Number of game rounds")]
	public int NumberOfRounds { get; set; }

	[Option("timecontrol", Required = true, HelpText = timeControlHelpText)]
	public string TimeControl { get; set; }

	[Option("setoption", Required = false, Separator = ',', HelpText = "Set options for the chess engines.")]
	public IEnumerable<string> SetOptions { get; set; }

	[Option("tuningfile", Required = false, HelpText = "Path to the CSV file containing tuning parameters.")]
	public string TuningParametersFile { get; set; }
}

class Program
{
	static void Main(string[] args)
	{
		Parser.Default.ParseArguments<Options>(args)
			.WithParsed(options =>
			{
				Duel duel = new();

				string engine1Path = options.Engine1Path;
				string engine2Path = options.Engine2Path;

				duel.SetSPRT(options.Alpha, options.Beta, options.Elo0, options.Elo1);

				int numberOfThreads = options.NumberOfThreads;
				int rounds = options.NumberOfRounds;

				List<(string, (double, double))> opts = new();

				List<(string, double)> engineOptions = options.SetOptions
					.Select(option => option.Split('='))
					.Where(pair => pair.Length == 2)
					.Select(pair =>
					{
						string optionName = pair[0];
						double optionValue;
						double.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out optionValue);
						return (optionName, optionValue);
					})
					.ToList();

				foreach (var op in engineOptions)
				{
					opts.Add((op.Item1, (op.Item2, 0.0)));
				}

				foreach (var opt in engineOptions)
				{
					Console.WriteLine($"{opt.Item1}, {opt.Item2}");
				}

				if (!TryParseTimeControl(options.TimeControl, out int initialTime, out int increment))
				{
					Console.WriteLine("Failed to parse time control. Please provide a valid format (e.g., '60+1').");
				}

				if (!string.IsNullOrEmpty(options.TuningParametersFile))
				{
					LoadTuningParametersFromCsv(options.TuningParametersFile, out var tuningOptions);

					foreach (var opt in tuningOptions)
					{
						Console.WriteLine($"{opt.Item1}, {opt.Item2}, {opt.Item3}");
					}

				}
				else
				{
					duel.Run(engine1Path, engine2Path, numberOfThreads, initialTime, increment, rounds, engineOptions, new());
				}
			})
			.WithNotParsed(errors =>
			{
				Console.WriteLine("Failed to parse command-line arguments.");
			});
	}

	private static void LoadTuningParametersFromCsv(string filePath, out List<(string, double, double)> tuningOptions)
	{
		tuningOptions = new List<(string, double, double)>();

		try
		{
			var lines = File.ReadAllLines(filePath);

			foreach (var line in lines.Skip(1)) // Skip header line
			{
				var parts = line.Split(',');
				if (parts.Length == 3)
				{
					var option = parts[0].Trim();
					if (double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var defaultValue) &&
						double.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var stepValue))
					{
						tuningOptions.Add((option, defaultValue, stepValue));
					}
					else
					{
						Console.WriteLine($"Invalid numeric values in CSV file: {line}");
					}
				}
				else
				{
					Console.WriteLine($"Invalid line in CSV file: {line}");
				}
			}
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error loading tuning parameters from CSV file: {ex.Message}");
		}
	}

	private static bool TryParseTimeControl(string timeControl, out int initialTime, out int increment)
	{
		initialTime = 0;
		increment = 0;

		double secondsTime = 0;
		double secondsIncrement = 0;

		string[] parts = timeControl.Split('+');

		if (parts.Length == 2 &&
			double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out secondsTime) &&
			double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out secondsIncrement))
		{
			initialTime = (int)(secondsTime * 1000); // Convert seconds to milliseconds
			increment = (int)(secondsIncrement * 1000); // Convert seconds to milliseconds
			return true;
		}

		return false;
	}
}
