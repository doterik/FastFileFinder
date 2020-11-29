//#pragma warning disable IDE0003 // Remove qualification
#pragma warning disable IDE0007 // Use implicit type
//#pragma warning disable IDE0049 // Simplify Names

// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FastFindArgumentParser.cs" company="John Robbins/Wintellect">
//   (c) 2012-2016 by John Robbins/Wintellect
// </copyright>
// <summary>
//   The fast file finder program.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace NativeFindFile
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Text;
	using System.Text.RegularExpressions;
	using NativeFindFile.Properties;

	/// <summary>
	/// Implements the command line parsing for the Fast Find program.
	/// </summary>
	internal sealed class FastFindArgumentParser : ArgParser
	{
		/// <summary>
		/// The path flag.
		/// </summary>
		private const string PathFlag = "path";

		/// <summary>
		/// The path flag short.
		/// </summary>
		private const string PathFlagShort = "p";

		/// <summary>
		/// The use regular expressions flag.
		/// </summary>
		private const string RegExFlag = "regex";

		/// <summary>
		/// The short use regular expressions flag.
		/// </summary>
		private const string RegExFlagShort = "re";

		/// <summary>
		/// The only files flag.
		/// </summary>
		private const string IncludeDirectoryName = "includedir";

		/// <summary>
		/// The short only files flag short.
		/// </summary>
		private const string IncludeDirectoryNameShort = "i";

		/// <summary>
		/// The no statistics flag.
		/// </summary>
		private const string NoStats = "nostats";

		/// <summary>
		/// The short no stats flag.
		/// </summary>
		private const string NoStatsShort = "ns";

		/// <summary>
		/// The help flag.
		/// </summary>
		private const string HelpFlag = "help";

		/// <summary>
		/// The short help flag.
		/// </summary>
		private const string HelpFlagShort = "?";

		/// <summary>
		/// The raw patterns as they come in from the command line.
		/// </summary>
		private readonly List<string> rawPatterns = new();

		/// <summary>
		/// The private string to hold more detailed error information.
		/// </summary>
		private string errorMessage = string.Empty;

		/// <summary>Wheter to use regular expressions or not.</summary>
		private bool useRegEx;

		/// <summary>
		/// Initializes a new instance of the <see cref="FastFindArgumentParser"/> class.
		/// </summary>
		public FastFindArgumentParser()
			: base(
				new[] { RegExFlag, RegExFlagShort, IncludeDirectoryName, IncludeDirectoryNameShort, NoStats, NoStatsShort, HelpFlagShort },
				new[] { PathFlag, PathFlagShort },
				false)
		{
			//this.Path = String.Empty;
			//this.useRegEx = false;
			//this.IncludeDirectories = false;
			//this.NoStatistics = false;
			//this.Patterns = new();
			//this.rawPatterns = new();
			//this.errorMessage = String.Empty;
		}

		/// <summary>
		/// Gets the path to search. The default is the current directory.
		/// </summary>
		public string Path { get; private set; } = string.Empty;

		/// <summary>
		/// Gets a value indicating whether the user only wants to include the
		/// directory name as part of the search.
		/// </summary>
		public bool IncludeDirectories { get; private set; }

		/// <summary>
		/// Gets a value indicating whether the user wants to see the final
		/// search stats.
		/// </summary>
		public bool NoStatistics { get; private set; }

		/// <summary>
		/// Gets the patterns to search for.
		/// </summary>
		public List<Regex> Patterns { get; } = new();

		/// <summary>
		/// Reports correct command line usage.
		/// </summary>
		/// <param name="errorInfo">
		/// The string with the invalid command line option.
		/// </param>
		public override void OnUsage(string? errorInfo)
		{
			ProcessModule exe = Process.GetCurrentProcess().Modules[0];
			Console.WriteLine(Constants.UsageString, exe.FileVersionInfo.FileVersion);

			if (!string.IsNullOrEmpty(errorInfo)) Program.WriteError(Constants.ErrorSwitch, errorInfo);
			if (!string.IsNullOrEmpty(errorMessage)) Program.WriteError(errorMessage);
		}

		/// <summary>
		/// Called when a switch is parsed out.
		/// </summary>
		/// <param name="switchSymbol">
		/// The switch value parsed out.
		/// </param>
		/// <param name="switchValue">
		/// The value of the switch. For flag switches this is null/Nothing.
		/// </param>
		/// <returns>
		/// One of the <see cref="ArgParser.SwitchStatus"/> values.
		/// </returns>
		protected override SwitchStatus OnSwitch(string switchSymbol, string? switchValue)
		{
			var ss = SwitchStatus.NoError;

			switch (switchSymbol)
			{
				case PathFlag:
				case PathFlagShort: ss = TestPath(switchValue); break;

				case RegExFlag:
				case RegExFlagShort: useRegEx = true; break;

				case IncludeDirectoryName:
				case IncludeDirectoryNameShort: IncludeDirectories = true; break;

				case NoStats:
				case NoStatsShort: NoStatistics = true; break;

				case HelpFlag:
				case HelpFlagShort: ss = SwitchStatus.ShowUsage; break;

				default:
					ss = SwitchStatus.Error;
					errorMessage = Constants.UnknownCommandLineOption;
					break;
			}

			return ss;
		}

		/// <summary>Called when a non-switch value is parsed out.</summary>
		/// <param name="value">The value parsed out.</param>
		/// <returns>One of the <see cref="ArgParser.SwitchStatus"/> values.</returns>
		protected override SwitchStatus OnNonSwitch(string value)
		{
			rawPatterns.Add(value); // Just add this to the list of patterns to search for.
			return SwitchStatus.NoError;
		}

		/// <summary>Called when parsing is finished so final sanity checking can be performed.</summary>
		/// <returns>One of the <see cref="ArgParser.SwitchStatus"/> values.</returns>
		protected override SwitchStatus OnDoneParse()
		{
			var ss = SwitchStatus.NoError;

			if (string.IsNullOrEmpty(Path)) Path = Directory.GetCurrentDirectory();

			if (rawPatterns.Count == 0) // The only error we can have is no patterns.
			{
				errorMessage = Constants.NoPatternsSpecified;
				ss = SwitchStatus.Error;
			}
			else
			{
				for (int i = 0; i < rawPatterns.Count; i++) // Convert all the raw patterns into regular expressions.
				{
					var thePattern = useRegEx
						? rawPatterns[i]
						: $"^{Regex.Escape(rawPatterns[i]).Replace("\\*", ".*").Replace("\\?", ".")}$";
					try
					{
						var rx = new Regex(thePattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
						Patterns.Add(rx);
					}
					catch (ArgumentException e)
					{
						// There was an error converting the command line parameter into a regular expression.
						// This happens when the user specified the -regex switch and they used a DOS wildcard pattern like *..
						var sb = new StringBuilder();
						sb.AppendFormat(CultureInfo.CurrentCulture, Constants.InvalidRegExFmt, thePattern, e.Message);
						errorMessage = sb.ToString();
						ss = SwitchStatus.Error;
						break;
					}
				}
			}

			return ss;
		}

		/// <summary>Isolates the checking for the path parameter.</summary>
		/// <param name="pathToTest">The path value to test.</param>
		/// <returns>A valid <see cref="SwitchStatus"/> value.</returns>
		private SwitchStatus TestPath(string? pathToTest)
		{
			if (!string.IsNullOrEmpty(Path)) errorMessage = Constants.PathMultipleSwitches;
			else if (!Directory.Exists(pathToTest)) errorMessage = Constants.PathNotExist;
			else
			{
				Path = pathToTest;
				return SwitchStatus.NoError; // Ok, time to exit.
			}

			return SwitchStatus.Error;
		}
	}
}
