using EngineDuel;
using CommandLine;

class Options
{
    [Option("engine1", Required = true, HelpText = "Path to the first chess engine.")]
    public string Engine1Path { get; set; }

    [Option("engine2", Required = true, HelpText = "Path to the second chess engine.")]
    public string Engine2Path { get; set; }

    [Option("alpha", Required = true, Default = 0.05, HelpText = "Alpha value for SPRT.")]
    public double Alpha { get; set; }

    [Option("beta", Required = true, Default = 0.05, HelpText = "Beta value for SPRT.")]
    public double Beta { get; set; }

    [Option("elo0", Required = true, Default = 0, HelpText = "Elo0 value for SPRT.")]
    public double Elo0 { get; set; }

    [Option("elo1", Required = true, Default = 5, HelpText = "Elo1 value for SPRT.")]
    public double Elo1 { get; set; }

    [Option("threads", Required = false, Default = 12, HelpText = "Number of threads.")]
    public int NumberOfThreads { get; set; }
}

class Program  {  
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

                duel.Run(engine1Path, engine2Path, numberOfThreads);
            })
            .WithNotParsed(errors => 
            {
                Console.WriteLine("Failed to parse command-line arguments.");
            });
    }
}
