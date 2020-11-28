#pragma warning disable IDE0049 // Simplify Names

namespace NativeFindFile
{
	using System;
	using System.IO;
	using System.Runtime.InteropServices;
	using System.Runtime.InteropServices.ComTypes;

	/// <summary>
	/// Wrappers around all native Win32 API calls.
	/// </summary>
	internal static class NativeMethods
	{
		internal enum FINDEX_INFO_LEVELS
		{
			Standard = 0,
			Basic = 1
		}

		internal enum FINDEX_SEARCH_OPS
		{
			SearchNameMatch = 0,
			SearchLimitToDirectories = 1,
			SearchLimitToDevices = 2
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		internal struct WIN32_FIND_DATA
		{
			public FileAttributes dwFileAttributes;
			public FILETIME ftCreationTime;
			public FILETIME ftLastAccessTime;
			public FILETIME ftLastWriteTime;
			public UInt32 nFileSizeHigh;
			public UInt32 nFileSizeLow;
			public readonly UInt32 dwReserved0;
			private readonly UInt32 dwReserved1;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] // TODO: '\\?\C:\...' 260?
			public String cFileName;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
			public String cAlternateFileName;
		}

		internal enum FindExAdditionalFlags
		{
			None = 0,
			CaseSensitive = 1,
			LargeFetch = 2
		}

		[DllImport("kernel32.dll", SetLastError = false, CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern Boolean FindClose(IntPtr hFindFile);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "FindFirstFileExW")]
		internal static extern SafeFindFileHandle FindFirstFileEx([MarshalAs(UnmanagedType.LPWStr)] String lpFileName,
																  FINDEX_INFO_LEVELS fInfoLevelId,
																  out WIN32_FIND_DATA lpFindFileData,
																  FINDEX_SEARCH_OPS fSearchOp,
																  IntPtr lpSearchFilter,
																  FindExAdditionalFlags dwAdditionalFlags);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "FindNextFileW")]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern Boolean FindNextFile(SafeFindFileHandle hFindFile, out WIN32_FIND_DATA lpFindFileData);


	}
}
