// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="John Robbins/Wintellect">
//   (c) 2012-2016 by John Robbins/Wintellect
// </copyright>
// <summary>
//   The fast file finder program.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
#pragma warning disable IDE0007 // Use implicit type
//#pragma warning disable IDE0049 // Simplify Names
#pragma warning disable IDE0063 // Use simple 'using' statement

namespace NativeFindFile
{
	using System;
	using System.Collections.Concurrent;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Globalization;
	using System.IO;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using NativeFindFile.Properties;

	/// <summary>
	/// The entry point to the application.
	/// </summary>
	public sealed class Program // internal
	{
		/// <summary>
		/// Holds the command line options the user wanted.
		/// </summary>
		private static readonly FastFindArgumentParser Options = new();

		/// <summary>
		/// The total number of matching files and directories.
		/// </summary>
		private static long totalMatches;

		/// <summary>
		/// The total number of bytes the matching file consume.
		/// </summary>
		private static long totalMatchesSize;

		/// <summary>
		/// The total number of files looked at.
		/// </summary>
		private static long totalFiles;

		/// <summary>
		/// The total number of directories looked at.
		/// </summary>
		private static long totalDirectories;

		/// <summary>
		/// The total number of reparse points.
		/// </summary>
		private static long totalReparses;

		/// <summary>
		/// The total number of bytes the files looked at consume.
		/// </summary>
		private static long totalSize;

		/// <summary>
		/// The collection to hold found strings so they can be printed in batch mode.
		/// </summary>
		private static readonly BlockingCollection<string> ResultsQueue = new();

		/// <summary>
		/// The entry point function for the program.
		/// </summary>
		/// <param name="args">
		/// The command line arguments for the program.
		/// </param>
		/// <returns>
		/// 0 - Proper execution
		/// 1 - Invalid command line.
		/// </returns>
		public static int Main(string[] args) // internal
		{
			int returnValue = 0;

			//totalMatches = 0;
			//totalMatchesSize = 0;
			//totalFiles = 0;
			//totalDirectories = 0;
			//totalReparses = 0;
			//totalSize = 0;

			var timer = Stopwatch.StartNew(); // Have to include the time for parsing and creating the regular expressions.

			var parsed = Options.Parse(args);

			if (parsed)
			{
				using (var canceller = new CancellationTokenSource())
				{
					// Fire up the searcher and batch output threads.
					var task = Task.Factory.StartNew(() => RecurseFiles(Options.Path));
					var resultsTask = Task.Factory.StartNew(() => WriteResultsBatched(canceller.Token, 200));

					task.Wait();

					// Indicate a cancel so all remaining strings get printed out.
					canceller.Cancel();
					resultsTask.Wait();
				}

				timer.Stop();

				if (Options.NoStatistics == false)
				{
					Console.WriteLine(Constants.TotalTimeFmt, timer.ElapsedMilliseconds.ToString("N0", CultureInfo.CurrentCulture));
					Console.WriteLine(Constants.TotalFilesFmt, totalFiles.ToString("N0", CultureInfo.CurrentCulture));
					Console.WriteLine(Constants.TotalDirectoriesFmt, totalDirectories.ToString("N0", CultureInfo.CurrentCulture));
					Console.WriteLine(Constants.TotalSizeFmt, totalSize.ToString("N0", CultureInfo.CurrentCulture));
					Console.WriteLine(Constants.TotalMatchesFmt, totalMatches.ToString("N0", CultureInfo.CurrentCulture));
					Console.WriteLine(Constants.TotalMatchesSizeFmt, totalMatchesSize.ToString("N0", CultureInfo.CurrentCulture));
				}
			}
			else
			{
				returnValue = 1;
			}

			return returnValue;
		}

		/// <summary>
		/// Writes a error message to the screen.
		/// </summary>
		/// <param name="message">
		/// The message to report.
		/// </param>
		/// <param name="args">
		/// Any additional items to include in the output.
		/// </param>
		internal static void WriteError(string message, params object[] args) => ColorWriteLine(ConsoleColor.Red, message, args);

		/// <summary>
		/// Writes an error message to the screen.
		/// </summary>
		/// <param name="message">
		/// The message to write.
		/// </param>
		internal static void WriteError(string message) => ColorWriteLine(ConsoleColor.Red, message, null);

		/// <summary>
		/// Writes the text out in the specified foreground color.
		/// </summary>
		/// <param name="color">
		/// The foreground color to use.
		/// </param>
		/// <param name="message">
		/// The message to display.
		/// </param>
		/// <param name="args">
		/// Optional insertion arguments.
		/// </param>
		private static void ColorWriteLine(ConsoleColor color, string message, params object[]? args)
		{
			ConsoleColor currForeground = Console.ForegroundColor;
			try
			{
				Console.ForegroundColor = color;
				if (args != null)
				{
					Console.WriteLine(message, args);
				}
				else
				{
					Console.WriteLine(message);
				}
			}
			finally
			{
				Console.ForegroundColor = currForeground;
			}
		}

		/// <summary>Checks to see if the name matches and of the patterns.</summary>
		/// <param name="name">The name to match.</param>
		/// <returns>True if yes, false otherwise.</returns>
		private static bool IsNameMatch(string name)
		{
			for (int i = 0; i < Options.Patterns.Count; i++)
			{
				if (Options.Patterns[i].IsMatch(name))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>Takes care of writing out results found in a batch manner so slow calls to Console.WriteLine are minimized.</summary>
		/// <param name="canceller">The cancellation token.</param>
		/// <param name="batchSize">The batch size for the number of lines to write.</param>
		private static void WriteResultsBatched(CancellationToken canceller, int batchSize = 10)
		{
			var sb = new StringBuilder(batchSize * 260);
			var lineCount = 0;

			try
			{
				foreach (var line in ResultsQueue.GetConsumingEnumerable(canceller))
				{
					sb.AppendLine(line);
					lineCount++;

					if (lineCount > batchSize)
					{
						Console.Write(sb);
						sb.Clear();
						lineCount = 0;
					}
				}
			}
			catch (OperationCanceledException)
			{
				// Not much to do here.
			}
			finally
			{
				if (sb.Length > 0)
				{
					Console.Write(sb);
				}
			}
		}

		/// <summary>The method to call when a matching file/directory is found.</summary>
		/// <param name="line">The matching item to add to the output queue.</param>
		private static void QueueConsoleWriteLine(string line) => ResultsQueue.Add(line);

		/// <summary>The main method that does the recursive file matching.</summary>
		/// <param name="directory">The file directory to search.</param>
		/// <remarks>This method calls the low level Windows API because the built in .NET APIs do not support long file names. (Those greater than 260 characters).</remarks>
		private static void RecurseFiles(string directory)
		{
			string fileName = directory.StartsWith(@"\\")
				? directory.Replace(@"\\", @"\\?\UNC\") + @"\*" // TODO: Replace all occurrences?
				: @"\\?\" + directory + @"\*";

			using (SafeFindFileHandle fileHandle = NativeMethods.FindFirstFileEx(fileName,
																				 NativeMethods.FINDEX_INFO_LEVELS.Basic,
																				 out NativeMethods.WIN32_FIND_DATA findFileData,
																				 NativeMethods.FINDEX_SEARCH_OPS.SearchNameMatch,
																				 IntPtr.Zero,
																				 NativeMethods.FINDEX_ADDITIONAL_FLAGS.LargeFetch))
			{
				if (!fileHandle.IsInvalid)
				{
					do
					{
						if (findFileData.cFileName.Length <= 2 // Does this match "." or ".."?
							&& (findFileData.cFileName == "." ||
								findFileData.cFileName == "..")) continue; // If so, get out.

						if ((findFileData.dwFileAttributes & FileAttributes.Directory) == FileAttributes.Directory) // Directory? If so, queue up another task.
						{
							Interlocked.Increment(ref totalDirectories);

							if ((findFileData.dwFileAttributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint) // Reparse? If so, get out.
							{
								Interlocked.Increment(ref totalReparses); // TODO: Move if 'reparse' above directory test !?
								continue;
							}

							string subDirectory = directory + "\\" + findFileData.cFileName; // No need for 'Path.Combine()'.
							if (Options.IncludeDirectories)
							{
								if (IsNameMatch(findFileData.cFileName))
								{
									Interlocked.Increment(ref totalMatches);
									QueueConsoleWriteLine(subDirectory);
								}
							}

							Task.Factory.StartNew(() => RecurseFiles(subDirectory), TaskCreationOptions.AttachedToParent); // Recurse our way to happiness....
						}
						else // It's a file so look at it.
						{
							Interlocked.Increment(ref totalFiles);

							long fileSize = ((long)findFileData.nFileSizeHigh << 32) + findFileData.nFileSizeLow;
							Interlocked.Add(ref totalSize, fileSize);

							string fullFile = directory.EndsWith("\\") // No need for 'Path.Combine()'.
								? directory + findFileData.cFileName
								: directory + "\\" + findFileData.cFileName;

							string matchName = Options.IncludeDirectories ? fullFile : findFileData.cFileName;

							if (IsNameMatch(matchName))
							{
								Interlocked.Add(ref totalMatchesSize, fileSize);
								Interlocked.Increment(ref totalMatches);
								QueueConsoleWriteLine(fullFile);
							}
						}
					} while (NativeMethods.FindNextFile(fileHandle, out findFileData));

					/*	dwError = GetLastError();
						if (dwError != ERROR_NO_MORE_FILES)
						{
							DisplayErrorBox(TEXT("FindFirstFile"));
						}
						FindClose(hFind);
						return dwError; */
				}
			}
		}
	}
}
