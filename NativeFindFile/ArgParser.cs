//#pragma warning disable IDE0003 // Remove qualification
#pragma warning disable IDE0007 // Use implicit type
//#pragma warning disable IDE0049 // Simplify Names
#pragma warning disable SA1629 // Documentation text should end with a period
#pragma warning disable SA1642 // Constructor summary documentation should begin with standard text

//------------------------------------------------------------------------------
// <copyright file="ArgParser.cs" company="Wintellect">
//    Copyright (c) 2002-2016 John Robbins/Wintellect -- All rights reserved.
// </copyright>
// <Project>
//    Wintellect Debugging .NET Code
// </Project>
//------------------------------------------------------------------------------

namespace NativeFindFile
{
	using System;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using NativeFindFile.Properties;

	/// <summary>
	/// A command line argument parsing class.
	/// </summary>
	/// <remarks>
	/// This class is based on the WordCount version from the Framework SDK
	/// samples.  Any errors are mine.
	/// <para>
	/// There are two arrays of flags you'll pass to the constructors.  The
	/// flagSymbols are supposed to be standalone switches that toggle an option
	/// on.  The dataSymbols are for switches that take data values.  For
	/// example, if your application needs a switch, -c, to set the count,
	/// you'd put "c" in the dataSymbols.  This code will allow both "-c100" and
	/// the usual "-c" "100" both to be passed on the command line.  Note that
	/// you can pass null/Nothing for dataSymbols if you don't need them.
	/// </para>
	/// </remarks>
	internal abstract class ArgParser
	{
		/// <summary>For example: '/', '-'.</summary>
		private readonly char[] switchChars;

		/// <summary>Switch character(s) that are simple flags.</summary>
		private readonly string[] flagSymbols;

		/// <summary>Switch characters(s) that take parameters. For example: -f file. This can be null if not needed.</summary>
		private readonly string[] dataSymbols;

		/// <summary>Are switches case-sensitive?</summary>
		private readonly bool caseSensitiveSwitches;

		/// <summary>Initializes a new instance of the ArgParser class and defaults to
		/// "/" and "-" as the only valid switch characters.</summary>
		/// <param name="flagSymbols">The array of simple flags to toggle options on or off.</param>
		/// <param name="dataSymbols">The array of options that need data either in the next parameter or
		/// after the switch itself.  This value can be null/Nothing.</param>
		/// <param name="caseSensitiveSwitches">True if case sensitive switches are supposed to be used.</param>
		//[SuppressMessage("Microsoft.Naming",
		//			  	   "CA1726:UsePreferredTerms",
		//				   MessageId = "flag",
		//	  			   Justification = "Flag is appropriate term when dealing with command line arguments.")]
		protected ArgParser(string[] flagSymbols, string[] dataSymbols, bool caseSensitiveSwitches) : this(flagSymbols,
																											  dataSymbols,
																											  caseSensitiveSwitches,
																											  new[] { '/', '-' })
		{ }

		/// <summary>Initializes a new instance of the ArgParser class with all options specified by the caller.</summary>
		/// <param name="flagSymbols">The array of simple flags to toggle options on or off.</param>
		/// <param name="dataSymbols">The array of options that need data either in the next parameter or after the switch itself.  This value can be null/Nothing.</param>
		/// <param name="caseSensitiveSwitches">True if case sensitive switches are supposed to be used.</param>
		/// <param name="switchChars">The array of switch characters to use.</param>
		/// <exception cref="ArgumentException"> Thrown if <paramref name="flagSymbols"/> or <paramref name="switchChars"/> are invalid.</exception>
		//[SuppressMessage("Microsoft.Naming",
		//				   "CA1726:UsePreferredTerms",
		//				   MessageId = "flag",
		//				   Justification = "Flag is appropriate term when dealing with command line arguments.")]
		protected ArgParser(string[] flagSymbols,
							string[] dataSymbols,
							bool caseSensitiveSwitches,
							char[] switchChars)
		{
			Debug.Assert(flagSymbols != null, "null != flagSymbols");
#if DEBUG
			if (flagSymbols != null) Debug.Assert(flagSymbols.Length > 0, "flagSymbols.Length > 0"); // Avoid assertion side effects in debug builds.
#endif
			if ((flagSymbols == null) || (flagSymbols.Length == 0)) throw new ArgumentException(Constants.ArrayMustBeValid, nameof(flagSymbols));

			Debug.Assert(switchChars != null, "null != switchChars");
#if DEBUG
			if (switchChars != null) Debug.Assert(switchChars.Length > 0, "switchChars.Length > 0"); // Avoid assertion side effects in debug builds.
#endif
			if ((switchChars == null) || (switchChars.Length == 0)) throw new ArgumentException(Constants.ArrayMustBeValid, nameof(switchChars));

			this.flagSymbols = flagSymbols;
			this.dataSymbols = dataSymbols;
			this.caseSensitiveSwitches = caseSensitiveSwitches;
			this.switchChars = switchChars;
		}

		/// <summary>The <cref="SwitchStatus"/> values for various internal methods.</summary>
		protected enum SwitchStatus
		{
			/// <summary>Indicates all parsing was correct.</summary>
			NoError,

			/// <summary>There was a problem.</summary>
			Error,

			/// <summary>Show the usage help.</summary>
			ShowUsage
		}

		/// <summary>Reports correct command line usage.</summary>
		/// <param name="errorInfo">The string with the invalid command line option.</param>
		public abstract void OnUsage(string? errorInfo); // TODO Null?

		/// <summary>Parses an arbitrary set of arguments.</summary>
		/// <param name="args">The string array to parse through.</param>
		/// <returns>True if parsing was correct.</returns>
		/// <exception cref="ArgumentException"> Thrown if <paramref name="args"/> is null.</exception>
		public bool Parse(string[] args)
		{
			var ss = SwitchStatus.NoError; // Assume parsing is successful.

			Debug.Assert(args != null, "null != args");
			_ = args ?? throw new ArgumentException(Constants.InvalidParameter);

			if (args.Length == 0) ss = SwitchStatus.ShowUsage; // Handle the easy case of no arguments.

			var errorArg = -1;
			int currArg;
			for (currArg = 0; (ss == SwitchStatus.NoError) && (currArg < args.Length); currArg++)
			{
				errorArg = currArg;

				var isSwitch = StartsWithSwitchChar(args[currArg]); // Determine if this argument starts with a valid switch character.

				if (!isSwitch) ss = OnNonSwitch(args[currArg]); // This is not a switch, notified the derived class of this "non-switch value".
				else
				{
					var useDataSymbols = false; // Indicates the symbol is a data symbol.

					var processedArg = args[currArg][1..]; // Get the argument itself.
					int n = IsSwitchInArray(flagSymbols, processedArg);

					if ((n == -1) && (dataSymbols != null)) // If it's not in the flags array, try the data array if that array is not null.
					{
						n = IsSwitchInArray(dataSymbols, processedArg);
						useDataSymbols = true;
					}

					if (n != -1)
					{
						string theSwitch;
						string? dataValue = null;

						// If it's a flag switch.
						if (!useDataSymbols) theSwitch = flagSymbols[n]; // This is a legal switch, notified the derived class of this switch and its value.
						else
						{
							theSwitch = dataSymbols is not null ? dataSymbols[n] : string.Empty; // TODO: Empty?

							if (currArg + 1 < args.Length) // Look at the next parameter if it's there.
							{
								dataValue = args[++currArg];

								// Take a look at dataValue to see if it starts with a switch character.
								// If it does, that means this data argument is empty.
								if (StartsWithSwitchChar(dataValue))
								{
									ss = SwitchStatus.Error;
									break;
								}
							}
							else
							{
								ss = SwitchStatus.Error;
								break;
							}
						}

						ss = OnSwitch(theSwitch, dataValue);
					}
					else
					{
						ss = SwitchStatus.Error;
						break;
					}
				}
			}

			// Finished parsing arguments
			if (ss == SwitchStatus.NoError) ss = OnDoneParse(); // No error occurred while parsing, let derived class perform a sanity check and return an appropriate status

			if (ss == SwitchStatus.ShowUsage) OnUsage(null); // Status indicates that usage should be shown, show it

			if (ss == SwitchStatus.Error)
			{
				var errorValue = (errorArg != -1) && (errorArg != args.Length) ? args[errorArg] : null;

				OnUsage(errorValue); // Status indicates that an error occurred, show it and the proper usage
			}

			return ss == SwitchStatus.NoError; // Return whether all parsing was successful.
		}

		/// <summary>Called when a switch is parsed out.</summary>
		/// <param name="switchSymbol">The switch value parsed out.</param>
		/// <param name="switchValue">The value of the switch. For flag switches this is null/Nothing.</param>
		/// <returns>One of the <see cref="SwitchStatus" /> values.</returns>
		/// <remarks>Every derived class must implement an OnSwitch method or a switch is considered an error.</remarks>
		protected virtual SwitchStatus OnSwitch(string switchSymbol, string? switchValue) => SwitchStatus.Error; // TODO Null?

		/// <summary>Called when a non-switch value is parsed out.</summary>
		/// <param name="value">The value parsed out.</param>
		/// <returns>One of the <see cref="SwitchStatus" /> values.</returns>
		protected virtual SwitchStatus OnNonSwitch(string value) => SwitchStatus.Error;

		/// <summary>Called when parsing is finished so final sanity checking can be performed.</summary>
		/// <returns>One of the <see cref="SwitchStatus"/> values.</returns>
		protected virtual SwitchStatus OnDoneParse() => SwitchStatus.Error; // By default, we'll assume that all parsing was an error.

		/// <summary>Looks to see if the switch is in the array.</summary>
		/// <param name="switchArray">The switch array.</param>
		/// <param name="value">The value.</param>
		/// <returns>The index of the switch.</returns>
		private int IsSwitchInArray(string[] switchArray, string value)
		{
			var valueCompare = caseSensitiveSwitches ? value.ToUpperInvariant() : value; // TODO: true/false !?

			for (var n = 0; n < switchArray.Length; n++)
			{
				if (string.CompareOrdinal(valueCompare, caseSensitiveSwitches ? switchArray[n].ToUpperInvariant() : switchArray[n]) == 0) return n;
			}

			return -1;
		}

		/// <summary>Looks to see if this string starts with a switch character.</summary>
		/// <param name="value">The string to check.</param>
		/// <returns>True if the string starts with a switch character.</returns>
		private bool StartsWithSwitchChar1(string value)
		{
			var isSwitch = false;
			for (int n = 0; !isSwitch && (n < switchChars.Length); n++) // TODO: foreach!
			{
				if (string.CompareOrdinal(value, 0, $"{switchChars[n]}", 0, 1) == 0)
				{
					isSwitch = true; // TODO: Return!
					break;
				}
			}

			return isSwitch;
		}

		private bool StartsWithSwitchChar(string value)
		{
			foreach (var switchChar in switchChars) if (value[0] == switchChar) return true;

			return false;
		}
	}
}
