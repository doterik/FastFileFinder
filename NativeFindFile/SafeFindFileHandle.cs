#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0049 // Simplify Names

namespace NativeFindFile
{
	using System;

	//using System.Diagnostics.CodeAnalysis;
	using Microsoft.Win32.SafeHandles;

	/// <summary>Wraps up the FindFirstFileEx and FindNextFile handle.</summary>
	internal sealed class SafeFindFileHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		//private SafeFindFileHandle() : base(true) { }

		//[SuppressMessage("Microsoft.Performance",
		//                 "CA1811:AvoidUncalledPrivateCode",
		//                 Justification = "Not called directly, but implicitly by FindFirstFileEx ")]

		/// <summary>Initializes a new instance of the <see cref="SafeFindFileHandle" /> class.</summary>
		/// <param name="handle">The handle.</param>
		/// <param name="ownsHandle">if set to <c>true</c> [owns handle].</param>
		public SafeFindFileHandle(IntPtr handle, Boolean ownsHandle = true) : base(ownsHandle) => SetHandle(handle);

		/// <summary>When overridden in a derived class, executes the code required to free the handle.</summary>
		/// <returns>
		///   <span class="keyword">true</span> if the handle is released successfully; otherwise, in the event of a catastrophic failure,
		///   <span class="keyword">false</span>. In this case, it generates a releaseHandleFailed Managed Debugging Assistant.
		/// </returns>
		protected override Boolean ReleaseHandle()
		{
			Boolean retValue = true;

			if (!IsClosed)
			{
				retValue = NativeMethods.FindClose(handle);
				SetHandleAsInvalid();
			}

			return retValue;
		}
	}
}
