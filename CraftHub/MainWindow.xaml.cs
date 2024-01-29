using Microsoft.Win32;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.IO;

using EngineDuel;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows.Media;


namespace GuilloChess
{
	// Define a class to represent engine options
	public class EngineOption
	{
		public string Name { get; set; }
		public double MinValue { get; set; }
		public double MaxValue { get; set; }
	}

	public class TextBoxLogger : ILogger
	{
		private readonly TextBox _textBox;
		private readonly TextBox _scoreTextBox; 
		private bool _isOptimization = true;
		private CancellationTokenSource _cancellationTokenSource; // New field for cancellation

		public TextBoxLogger(TextBox textBox, TextBox scoreTextBox)
		{
			_textBox = textBox;
			_scoreTextBox = scoreTextBox; // Initialize the new TextBox
		}

		public void SetOptimization(bool isOptimization, CancellationTokenSource cancellationTokenSource)
		{
			_isOptimization = isOptimization;
			_cancellationTokenSource = cancellationTokenSource;
		}

		public void Log(string message)
		{
			// Check if the current thread is the UI thread
			if (_textBox.Dispatcher.CheckAccess())
			{
				_textBox.AppendText(message + "\n");

				// Check if the message contains "iteration number"
				if (_isOptimization && message.Contains("Iteration number"))
				{
					string? iterationLine = message.Split('\n').FirstOrDefault(line => line.Contains("Iteration number"));
					_scoreTextBox.Text = iterationLine;
				}
				
				if (!_isOptimization && message.Contains("Wins"))
				{
					_scoreTextBox.Text = message;
				}
			}
			else
			{
				// Use the Dispatcher to update the TextBox from the UI thread
				_textBox.Dispatcher.Invoke(() =>
				{
					_textBox.AppendText(message + "\n");

					// Check if the message contains "iteration number"
					if (_isOptimization && message.Contains("Iteration number"))
					{
						string? iterationLine = message.Split('\n').FirstOrDefault(line => line.Contains("Iteration number"));
						_scoreTextBox.Text = iterationLine;
					}
					
					if (!_isOptimization && message.Contains("Wins"))
					{
						_scoreTextBox.Text = message;
					}
				});
			}
		}
	}


	public partial class MainWindow : Window
	{
		private string enginePath1;
		private string enginePath2;
		private int numberOfThreads = 12; 
		private int numberOfRounds = 10; 
		private double gameTime = 10;
		private double gameIncrement = 0.1;
		private ObservableCollection<EngineOption> engineOptionsList;
		private CancellationTokenSource _cancellationTokenSource;

		private List<(string, double, double)> engineOptions;

		private ILogger logger;

		public MainWindow()
		{
			_cancellationTokenSource = new CancellationTokenSource();
			InitializeComponent();
			SetMaxThreadsSlider();
			logger = new TextBoxLogger(logsTextBox, scoreTextBox);

			// Initialize engineOptionsList
			engineOptionsList = new ObservableCollection<EngineOption>();
			engineOptionsDataGrid.ItemsSource = engineOptionsList;
		}

		private void SetMaxThreadsSlider()
		{
			threadsSlider.Maximum = Environment.ProcessorCount;
		}

		private void BrowseButton1_Click(object sender, RoutedEventArgs e)
		{
			enginePath1 = SelectEnginePath("Select Engine 1");
			exePath1TextBox.Text = enginePath1;
		}

		private void BrowseButton2_Click(object sender, RoutedEventArgs e)
		{
			enginePath2 = SelectEnginePath("Select Engine 2");
			exePath2TextBox.Text = enginePath2;
		}

		private async void RunButton_Click(object sender, RoutedEventArgs e)
		{
			if (string.IsNullOrEmpty(enginePath1) || string.IsNullOrEmpty(enginePath2))
			{
				MessageBox.Show("Please select both engine paths.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			_cancellationTokenSource = new CancellationTokenSource(); 

			if (TryRetrieveUserData(out numberOfThreads, out numberOfRounds, out gameTime, out gameIncrement, out engineOptions))
			{
				if (optimizationRadioButton.IsChecked == true)
				{
					((TextBoxLogger)logger).SetOptimization(true, _cancellationTokenSource);

					// Example: Creating an instance of the optimizer
					Optimizer optimizer = new Optimizer(enginePath1, enginePath2, numberOfThreads, numberOfRounds, (int)gameTime * 1000, (int)gameIncrement * 1000, engineOptions, logger);

					// Run optimization
					await Task.Run(() => optimizer.Optimize(_cancellationTokenSource));
				} 
				else
				{
					((TextBoxLogger)logger).SetOptimization(false, _cancellationTokenSource);

					Duel duel = new(logger);
					duel.SetSPRT(0.05, 0.05, 0, 5);
					await Task.Run(() => duel.Run(enginePath1, enginePath2, numberOfThreads, (int)gameTime * 1000, (int)gameIncrement * 1000, numberOfRounds, new(), new(), _cancellationTokenSource));
				}
			}
		}

		private void StopButton_Click(object sender, RoutedEventArgs e)
		{
			// Cancel the ongoing optimization or duel
			_cancellationTokenSource?.Cancel();
		}

		private void AddOptionButton_Click(object sender, RoutedEventArgs e)
		{
			engineOptionsList.Add(new EngineOption());
		}

		private void DeleteOptionButton_Click(object sender, RoutedEventArgs e)
		{

			if (engineOptionsDataGrid.SelectedItem != null)
			{
				if (engineOptionsDataGrid.SelectedItem is EngineOption selectedOption)
				{
					engineOptionsList.Remove(selectedOption);
				}
			}
		}


		private bool TryRetrieveUserData(out int numberOfThreads, out int numberOfRounds, out double gameTime, out double timeIncrement, out List<(string, double, double)> engineOptions)
		{
			bool successful = true;

			numberOfThreads = (int)threadsSlider.Value;

			if (!int.TryParse(roundsTextBox.Text, out numberOfRounds))
			{
				MessageBox.Show("Please enter a valid number for Rounds.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				successful = false;
			}

			if (!double.TryParse(timeControlTextBox.Text, out gameTime))
			{
				MessageBox.Show("Please enter a valid number for Time Control.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				successful = false;
			}

			if (!double.TryParse(timeIncrementTextBox.Text, out timeIncrement))
			{
				MessageBox.Show("Please enter a valid number for Time Increment.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				successful = false;
			}

			// Retrieve engine options from DataGrid
			engineOptions = new List<(string, double, double)>();
			foreach (var item in engineOptionsList)
			{
				engineOptions.Add((item.Name, item.MinValue, item.MaxValue));
			}

			return successful;
		}


		private string SelectEnginePath(string title)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Title = title,
				Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*"
			};

			if (openFileDialog.ShowDialog() == true)
			{
				return openFileDialog.FileName;
			}

			return null;
		}
	}
}
