/*
** 2004 May 22
**
** The author disclaims copyright to this source code.  In place of
** a legal notice, here is a blessing:
**
**    May you do good and not evil.
**    May you find forgiveness for yourself and forgive others.
**    May you share freely, never taking more than you give.
**
******************************************************************************
**
** This header file defines OS-specific features for Win32
*/
#ifndef _SQLITE_OS_WIN_H_
#define _SQLITE_OS_WIN_H_

#include <windows.h>
#include <winbase.h>

#ifdef _WIN32_WCE
typedef struct sqlitewce_lockdata_t sqlitewce_lockdata_t;
#endif

/*
** The OsFile structure is a operating-system independing representation
** of an open file handle.  It is defined differently for each architecture.
**
** This is the definition for Win32.
*/
typedef struct OsFile OsFile;
struct OsFile {
  HANDLE h;               /* Handle for accessing the file */
  unsigned char locktype; /* Type of lock currently held on this file */
  unsigned char isOpen;   /* True if needs to be closed */
  short sharedLockByte;   /* Randomly chosen byte used as a shared lock */
#ifdef _WIN32_WCE
  int delOnClose;         /* To delete file on close */
  WCHAR * wFilename;      /* filename (for delete & global name generation) */
# ifndef SQLITE_WCE_OMIT_FILELOCK
  HANDLE  hMux;           /* Named mutex handle */
  HANDLE  hMem;           /* Named memory file mapping handle */
  sqlitewce_lockdata_t * lockdata; /* shared locking data (map view) */
# endif //!SQLITE_WCE_OMIT_FILELOCK
#endif //_WIN32_WCE
};


#define SQLITE_TEMPNAME_SIZE (MAX_PATH+50)
#define SQLITE_MIN_SLEEP_MS 1

/*
** This are WIN32 API functions not present in WinCE.
** They are implemented in the "os_wince.c" file.
**/
#ifdef _WIN32_WCE
# define DeleteFileA				sqlitewce_DeleteFileA
# define GetFileAttributesA			sqlitewce_GetFileAttributesA
# define GetTempPathA				sqlitewce_GetTempPathA
# define GetFullPathNameA			sqlitewce_GetFullPathNameA
# define GetSystemTimeAsFileTime	sqlitewce_GetSystemTimeAsFileTime
BOOL sqlitewce_DeleteFileA( LPCSTR zFilename );
DWORD sqlitewce_GetFileAttributesA( LPCSTR lpFileName );
DWORD sqlitewce_GetTempPathA( DWORD bufLen, LPSTR buf );
DWORD sqlitewce_GetFullPathNameA( LPCSTR,DWORD,LPSTR,LPSTR* );
void sqlitewce_GetSystemTimeAsFileTime( LPFILETIME );
#endif

/*
** It seems WinCE 4.x (don't know about 5) implements localtime,
** but only in the MFC library.
** To avoid any problems I just use my own implementation.
** It should be safe, as this header is not included by normal
** programs, only by the SQLite library.
**/
#if _WIN32_WCE >= 400
# define localtime					sqlitewce_localtime
#endif
#endif /* _SQLITE_OS_WIN_H_ */
