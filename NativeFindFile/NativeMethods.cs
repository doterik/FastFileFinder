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
		/*	typedef enum _FINDEX_INFO_LEVELS {
			  FindExInfoStandard,
			  FindExInfoBasic,
			  FindExInfoMaxInfoLevel
			} FINDEX_INFO_LEVELS; */
		internal enum FINDEX_INFO_LEVELS
		{
			Standard = 0,
			Basic = 1 // Does not query the short file name, improving overall enumeration speed. The cAlternateFileName member is always a NULL string.
		}

		/*	typedef enum _FINDEX_SEARCH_OPS {
			  FindExSearchNameMatch,
			  FindExSearchLimitToDirectories,
			  FindExSearchLimitToDevices,
			  FindExSearchMaxSearchOp
			} FINDEX_SEARCH_OPS; */

		internal enum FINDEX_SEARCH_OPS
		{
			SearchNameMatch = 0,          // The search for a file that matches a specified file name.
			SearchLimitToDirectories = 1, // If the file system supports directory filtering, the function searches for a file that matches the specified name and is also a directory.
			SearchLimitToDevices = 2
		}

		internal enum FINDEX_ADDITIONAL_FLAGS
		{
			None = 0,
			CaseSensitive = 1,
			LargeFetch = 2 // Uses a larger buffer for directory queries, which can increase performance of the find operation.
		}

		/*	typedef struct _WIN32_FIND_DATAW {
			  DWORD    dwFileAttributes;
			  FILETIME ftCreationTime;
			  FILETIME ftLastAccessTime;
			  FILETIME ftLastWriteTime;
			  DWORD    nFileSizeHigh;
			  DWORD    nFileSizeLow;
			  DWORD    dwReserved0;
			  DWORD    dwReserved1;
			  WCHAR    cFileName[MAX_PATH];
			  WCHAR    cAlternateFileName[14];
			  DWORD    dwFileType;
			  DWORD    dwCreatorType;
			  WORD     wFinderFlags;
			} WIN32_FIND_DATAW, *PWIN32_FIND_DATAW, *LPWIN32_FIND_DATAW; */

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		internal struct WIN32_FIND_DATA
		{
			public FileAttributes dwFileAttributes;
			public FILETIME ftCreationTime;
			public FILETIME ftLastAccessTime;
			public FILETIME ftLastWriteTime;
			public UInt32 nFileSizeHigh;
			public UInt32 nFileSizeLow;
			private readonly UInt32 dwReserved0; // If the dwFileAttributes member includes the FILE_ATTRIBUTE_REPARSE_POINT attribute, this member specifies the reparse point tag.
			private readonly UInt32 dwReserved1;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] // TODO: '\\?\C:\...' 260?
			public String cFileName;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
			private readonly String cAlternateFileName; // Not used.
			private readonly UInt32 dwFileType;
			private readonly UInt32 dwCreatorType;
			private readonly UInt16 wFinderFlags;
		}

		[DllImport("kernel32.dll", SetLastError = false, CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern Boolean FindClose(IntPtr hFindFile);

		/*	HANDLE FindFirstFileExW(
				LPCWSTR            lpFileName,
				FINDEX_INFO_LEVELS fInfoLevelId,
				LPVOID             lpFindFileData,
				FINDEX_SEARCH_OPS  fSearchOp,
				LPVOID             lpSearchFilter,
				DWORD              dwAdditionalFlags);	*/

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "FindFirstFileExW")]
		internal static extern SafeFindFileHandle FindFirstFileEx([MarshalAs(UnmanagedType.LPWStr)] String lpFileName,
																  FINDEX_INFO_LEVELS fInfoLevelId,
																  out WIN32_FIND_DATA lpFindFileData,
																  FINDEX_SEARCH_OPS fSearchOp,
																  IntPtr lpSearchFilter,
																  FINDEX_ADDITIONAL_FLAGS dwAdditionalFlags);

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "FindNextFileW")]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern Boolean FindNextFile(SafeFindFileHandle hFindFile, out WIN32_FIND_DATA lpFindFileData);
	}
}
