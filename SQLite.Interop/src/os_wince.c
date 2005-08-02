extern "C"
{
/*
** 2005 April 1 - Nuno Lucas
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
** This file contains code that is specific to Windows CE.
*/
#include "sqliteInt.h"
#include "os.h"          /* Must be first to enable large file support */
#ifdef _WIN32_WCE        /* This file is used for Windows CE only */
#include <time.h>

/*
** Include code that is common to all os_*.c files
*/
#include "os_common.h"


/*
**
** Implementation of the assert function for systems not having it
**
** Very basic, just opens a message box displaying where and what fired the
** the assert failure.
*/
void sqlitewce_assert( int x, char * test, char * file, int line )
{
	/* This should be fixed somehow, to avoid overflows.
	 * Also, when an assert is caused by memory allocation faillure, this
	 * will probably fail.
	 */
	WCHAR buf[2048];
	if (x) return;
	swprintf( buf, L"assert( %hs )\r\n\r\nFile: '%hs'  Line: %d", test, file, line );
	MessageBoxW( 0, buf, L"Assertion Error", MB_ICONERROR );
}

/*
** Implementation of the localtime function for systems not having it.
** Convert time_t to local time in tm struct format.
*/
struct tm * sqlitewce_localtime( const time_t *timer )
{
	static struct tm s_tm;
	FILETIME	uf, lf;
	SYSTEMTIME	ls;
	// Convert time_t to FILETIME
	unsigned __int64 i64 = Int32x32To64(timer, 10000000) + 116444736000000000;
	uf.dwLowDateTime = (DWORD) i64;
	uf.dwHighDateTime = (DWORD) (i64 >> 32);
	// Convert UTC(GMT) FILETIME to local FILETIME
	FileTimeToLocalFileTime( &uf, &lf );
	// Convert FILETIME to SYSTEMTIME
	FileTimeToSystemTime( &lf, &ls );
	// Convert SYSTEMTIME to tm
	s_tm.tm_sec  = ls.wSecond;
	s_tm.tm_min  = ls.wMinute;
	s_tm.tm_hour = ls.wHour;
	s_tm.tm_mday = ls.wDay;
	s_tm.tm_mon  = ls.wMonth -1;
	s_tm.tm_year = ls.wYear - 1900;
	s_tm.tm_wday = ls.wDayOfWeek;
	// Return pointer to static data
	return &s_tm;
}

/*
** Similar to strdup, but first converts the MBCS string to UNICODE
** and then returns the UNICODE clone.
** Don't forget to free() the returned string.
** I assume a 2 byte size per character for unicode. That's what windows
** thinks unicode strings are (expect having to change this in 2010 ;)
*/
static WCHAR * StrDupW( const char * str )
{
	size_t size = strlen(str) + 1; // +1 for terminating '\0'
	WCHAR * aux = (WCHAR *) malloc( size*sizeof(WCHAR) );
	MultiByteToWideChar( CP_ACP, 0, str,-1, aux, size );
	return aux;
}

/*
** Windows CE versions prior to 3.0 don't implement atof(), so we
** implement it here as a wrapper to wcstod().
*/
double sqlitewce_atof( const char *str )
{
	wchar_t * aux = StrDupW( str );
	double d = wcstod( aux, NULL );
	free( aux );
	return d;
}

/*
** This is needed for the command line version of sqlite to compile.
**/
int isatty( int handle )
{
	UNREFERENCED_PARAMETER(handle);
	return 1;
}

/*
** Converts a relative path to an absolute path.
** There is no current directory concept on Windows CE, so we assume
** we are working always with absolute paths and simply copy
** the given path to the provided buffer.
*/
DWORD sqlitewce_GetFullPathNameA
	(
		LPCSTR	lpFileName,
		DWORD	nBufferLength,
		LPSTR	lpBuffer,
		LPSTR *	lpFilePart
	)
{
	DWORD i = 0;
	for ( ; i < nBufferLength; ++i )
	{
		lpBuffer[i] = lpFileName[i];
		if ( lpBuffer[i] == '\\' || lpBuffer[i] == '/' )
			*lpFilePart = lpBuffer + i + 1;
		if ( lpBuffer[i] == '\0' )
			break;
	}
	return (i >= nBufferLength)? strlen(lpFileName) + 1 : i;
}

/*
** Simple wrapper to the Unicode version of GetFileAttributes().
*/
DWORD sqlitewce_GetFileAttributesA( LPCSTR lpFileName )
{
	wchar_t * aux = StrDupW( lpFileName );
	DWORD ret = GetFileAttributesW( aux );
	free( aux );
	return ret;
}

/*
** WinCE doesn't implement GetSystemTimeAsFileTime(), but is
** trivial to code.
*/
void sqlitewce_GetSystemTimeAsFileTime( LPFILETIME ft )
{
	SYSTEMTIME st;
	GetSystemTime( &st );
	SystemTimeToFileTime( &st, ft );
}

/*
** Simple wrapper to the Unicode version of DeleteFile().
*/
BOOL sqlitewce_DeleteFileA( LPCSTR zFilename )
{
	wchar_t * aux = StrDupW( zFilename );
	BOOL ret = DeleteFileW( aux );
	free( aux );
	return ret;
}

/*
** Wrapper to the Unicode version of GetTempPath().
**
** NOTE: The MSDN says GetTempPath() can fail if no temporary path
**       defined. No check for this, as now is possible to define
**       an alternate temporary path for sqlite.
*/
DWORD sqlitewce_GetTempPathA( DWORD bufLen, LPSTR buf )
{
	int len = GetTempPathW( 0,0 );
	LPWSTR wTempPath = (LPWSTR) malloc( (len+1)*sizeof(WCHAR) );
	GetTempPathW( len+1, wTempPath );
	len = WideCharToMultiByte( CP_ACP, 0, wTempPath,-1, buf,bufLen, 0,0 );
	free( wTempPath );
	return len;
}


/**********************************************************************
 * File locking helper functions for Windows CE
 *********************************************************************/

#ifndef SQLITE_WCE_OMIT_FILELOCK

/*
** Structure holding the global locking data for each open database/file.
** sqlitewce_LockMutex() must be used before using this data.
**
** <lock> holds the global lock state of the file. Every time a process
**			holds a lock on the file, 1 << locktype is set, i.e., bit 1
**			if any process with a SHARED lock, bit 2 is set if any with
**			a RESERVED lock, bit 3 for the PENDING lock and bit 4 for
**			the EXCLUSIVE lock. bit 0 is ignored, and may be set or not
**          in between (if it simplifies the algorithm).
** <shared> is the count of processes holding the SHARED lock.
**
*/
typedef struct sqlitewce_lockdata_t
{
	unsigned	lock;	/* global lock state */
	unsigned	shared;	/* global share count */
} sqlitewce_lockdata_t;

/*
** Lock access to file locking data.
** Only returns on success.
**
** NOTE: I'm not sure in what conditions this can turn into an infinite
**       loop. I don't think it is possible without a serious bug in
**       sqlite or windows, but i'm not sure of this.
*/
static void lock_file( OsFile *id )
{
	DWORD res;
	while ( 1 )
	{
		res = WaitForSingleObject( id->hMux, INFINITE );
		// I don't know very well what I have to do in this case.
		// The MSDN says that this case is when a thread terminates without
		// releasing the mutex. So I have to release it and try again
		if ( res == WAIT_ABANDONED )
		{
			ReleaseMutex( id->hMux );
			continue;
		}
		// success ?
		if ( res == WAIT_OBJECT_0 )
			break;
		// Let the owner have time to release it
		Sleep( 1 );
	}
}

/*
** Releases ownership of the file mutex.
** Always success
*/
static void unlock_file( OsFile *id )
{
	ReleaseMutex( id->hMux );
}

/*
** Acquire a lock on the file.
** Returns non-zero on success, zero on failure.
*/
static int getLock( OsFile *id, int locktype )
{
	int rc = 0;
	lock_file( id );
	if ( locktype == SHARED_LOCK )
	{
		assert( id->lockdata->shared >= 0 );
		++id->lockdata->shared;	/* Increment number of readers */
		id->lockdata->lock |= 1 << SHARED_LOCK;
	}
	else
	{
		if ( id->lockdata->lock & (1 << locktype) )
		{
			unlock_file( id );
			return 0;	/* Already locked by others */
		}
		id->lockdata->lock |= 1 << locktype;
	}
	unlock_file( id );
	return 1;
}

/*
** Releases lock on the file.
** Always succeeds, so no return value.
*/
static void unsetLock( OsFile *id, int locktype )
{
	assert( locktype >= SHARED_LOCK );
	lock_file( id );
	if ( locktype == SHARED_LOCK )
	{
		assert( id->lockdata->shared > 0 );
		--id->lockdata->shared;	/* Decrement number of readers */
		if ( id->lockdata->shared == 0 )	/* Last reader? */
			id->lockdata->lock &= ~(1 << SHARED_LOCK);
	}
	else
	{
		id->lockdata->lock &= ~(1 << locktype);
	}
	unlock_file( id );
}

/*
** Initializes file locking struture.
** Returns non-zero on success, zero on error.
**
** Each open file will have an associated shared memory area, where the global
** lock information will be stored.
** The file path is used to generate a unique name for each file.
** An aditional global mutex per file is created, for syncronization between
** processes.
*/
static int sqlitewce_InitLocks( OsFile *id )
{
	WCHAR * aux;
	WCHAR muxName[256] = L"sqwce_mux_";
	WCHAR memName[256] = L"sqwce_mem_";
	int i, exists;

	// Generate resource names suffix from the file name
	aux = _wcsdup( id->wFilename );
	if ( aux == NULL )	return 0; // No mem
	for ( i = 0; aux[i]; ++i )
	{
		if ( aux[i] == '\\' )
			aux[i] = '/'; // can't use '\\' in name
		else
			aux[i] = towlower( aux[i] ); // names are case sensitive
	}
	wcsncat( muxName, aux, 256 );
	wcsncat( memName, aux, 256 );
	free( aux );

	// Create named mutex (or open existing)
	id->hMux = CreateMutex( NULL, FALSE, muxName );
	if ( id->hMux == NULL )
		return 0; // No mem or something weird

	// Lock access to file data (avoid race condition on create/open)
	lock_file( id );

	// Create shared memory mapping or open existing
	id->hMem = CreateFileMapping(
					INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE,
					0, sizeof(sqlitewce_lockdata_t),
					memName );
	if ( id->hMem == NULL )
	{
		unlock_file( id );
		CloseHandle( id->hMux );
		return 0;	// No mem or something weird
	}

	// Check if already exists (created by other process)
	exists = (GetLastError() == ERROR_ALREADY_EXISTS);

	// Open view to the data
	id->lockdata = (sqlitewce_lockdata_t *)MapViewOfFile( id->hMem, FILE_MAP_WRITE, 0,0, 0 );
	if ( id->lockdata == NULL )
	{
		unlock_file( id );
		CloseHandle( id->hMem );
		CloseHandle( id->hMux );
		return 0;	// No mem or something weird
	}

	// Initialize lockdata, if first time
	if ( ! exists )
		memset( id->lockdata, 0, sizeof(sqlitewce_lockdata_t) );

	// Done, release global lock on the file.
	unlock_file( id );

	return 1;
}

/*
** Releases any locks held on the file and releases locking data.
** Doesn't return anything, because there is no way to recover
** from a faillure to remove the lock (and a faillure to do so
** would be a bug either in windows or in sqlite).
*/
static void sqlitewce_ReleaseLocks( OsFile *id )
{
	if ( id->lockdata )
	{
		sqlite3OsUnlock( id, NO_LOCK );
		UnmapViewOfFile( id->lockdata );
		CloseHandle( id->hMem );
		CloseHandle( id->hMux );
	}
}

#endif // !defined(SQLITE_WCE_OMIT_FILELOCK)


/**********************************************************************
 * sqlite3Os functions implemented specificaly for WinCE
 *********************************************************************/

/*
** Attempt to open a file for both reading and writing.  If that
** fails, try opening it read-only.  If the file does not exist,
** try to create it.
**
** On success, a handle for the open file is written to *id
** and *pReadonly is set to 0 if the file was opened for reading and
** writing or 1 if the file was opened read-only.  The function returns
** SQLITE_OK.
**
** On failure, the function returns SQLITE_CANTOPEN and leaves
** *id and *pReadonly unchanged.
*/
int sqlite3OsOpenReadWrite( const char *zFilename, OsFile *id, int *pReadonly )
{
	HANDLE h;
	WCHAR *wFilename = StrDupW( zFilename );
	if ( wFilename == NULL )
		return SQLITE_NOMEM;
	assert( !id->isOpen );
	h = CreateFileW( wFilename, GENERIC_READ | GENERIC_WRITE,
					FILE_SHARE_READ | FILE_SHARE_WRITE,
					NULL, OPEN_ALWAYS,
					FILE_ATTRIBUTE_NORMAL | FILE_FLAG_RANDOM_ACCESS,
					NULL );
	if ( h == INVALID_HANDLE_VALUE )
	{
		h = CreateFileW( wFilename, GENERIC_READ,
					FILE_SHARE_READ,
					NULL, OPEN_ALWAYS,
					FILE_ATTRIBUTE_NORMAL | FILE_FLAG_RANDOM_ACCESS,
					NULL );
		if ( h == INVALID_HANDLE_VALUE )
		{
			free( wFilename );
			return SQLITE_CANTOPEN;
		}
		*pReadonly = 1;
	}
	else
	{
		*pReadonly = 0;
	}
	// Fill file context data
	id->h = h;
	id->locktype = NO_LOCK;
	id->sharedLockByte = 0;
	id->isOpen = 1;
	id->wFilename = wFilename;
	id->delOnClose = 0;
#ifndef SQLITE_WCE_OMIT_FILELOCK
	if ( ! sqlitewce_InitLocks(id) )
	{	// Failled to initialize file lock mechanism
		free( wFilename );
		CloseHandle( h );
		return SQLITE_NOMEM;
	}
#endif
	OpenCounter(+1);
	TRACE3("OPEN R/W %d \"%s\"\n", h, zFilename);
	return SQLITE_OK;
}

/*
** Attempt to open a new file for exclusive access by this process.
** The file will be opened for both reading and writing.  To avoid
** a potential security problem, we do not allow the file to have
** previously existed.  Nor do we allow the file to be a symbolic
** link.
**
** If delFlag is true, then make arrangements to automatically delete
** the file when it is closed.
**
** On success, write the file handle into *id and return SQLITE_OK.
**
** On failure, return SQLITE_CANTOPEN.
*/
int sqlite3OsOpenExclusive( const char *zFilename, OsFile *id, int delFlag )
{
	HANDLE h;
	WCHAR * wFilename = StrDupW( zFilename );
	assert( !id->isOpen );
	h = CreateFileW( wFilename, GENERIC_READ | GENERIC_WRITE, 0,
					NULL, CREATE_ALWAYS, FILE_FLAG_RANDOM_ACCESS, NULL );
	if ( h == INVALID_HANDLE_VALUE )
	{
		free( wFilename );
		return SQLITE_CANTOPEN;
	}
	id->h = h;
	id->locktype = NO_LOCK;
	id->sharedLockByte = 0;
	id->isOpen = 1;
	id->wFilename = wFilename;
	id->delOnClose = delFlag;
#ifndef SQLITE_WCE_OMIT_FILELOCK
	// Not shared, so no need to lock file (it would fail to open)
	id->hMux = NULL;
	id->hMem = NULL;
	id->lockdata = NULL;
#endif
	OpenCounter(+1);
	TRACE3("OPEN EX %d \"%s\"\n", h, zFilename);
	return SQLITE_OK;
}

/*
** Attempt to open a new file for read-only access.
**
** On success, write the file handle into *id and return SQLITE_OK.
**
** On failure, return SQLITE_CANTOPEN.
*/
int sqlite3OsOpenReadOnly( const char *zFilename, OsFile *id )
{
	HANDLE h;
	WCHAR * wFilename = StrDupW( zFilename );
	assert( !id->isOpen );
	h = CreateFileW( wFilename, GENERIC_READ, 0, NULL, OPEN_EXISTING,
					FILE_ATTRIBUTE_NORMAL | FILE_FLAG_RANDOM_ACCESS, NULL );
	if ( h == INVALID_HANDLE_VALUE )
	{
		free( wFilename );
		return SQLITE_CANTOPEN;
	}
	id->h = h;
	id->locktype = NO_LOCK;
	id->sharedLockByte = 0;
	id->isOpen = 1;
	id->wFilename = wFilename;
	id->delOnClose = 0;
#ifndef SQLITE_WCE_OMIT_FILELOCK
	// Not shared, so no need to lock file
	id->hMux = NULL;
	id->hMem = NULL;
	id->lockdata = NULL;
#endif
	OpenCounter(+1);
	TRACE3("OPEN RO %d \"%s\"\n", h, zFilename);
	return SQLITE_OK;
}

/*
** Close a file.
*/
int sqlite3OsClose( OsFile *id )
{
	if ( id->isOpen )
	{
		TRACE2("CLOSE %d\n", id->h);
#ifndef SQLITE_WCE_OMIT_FILELOCK
		sqlitewce_ReleaseLocks( id );
#endif
		CloseHandle(id->h);
		OpenCounter(-1);
		id->isOpen = 0;
		if ( id->delOnClose )
			DeleteFileW( id->wFilename );
		free( id->wFilename );
	}
	return SQLITE_OK;
}

/*
** Lock the file with the lock specified by parameter locktype - one
** of the following:
**
**     (1) SHARED_LOCK
**     (2) RESERVED_LOCK
**     (3) PENDING_LOCK
**     (4) EXCLUSIVE_LOCK
**
** Sometimes when requesting one lock state, additional lock states
** are inserted in between.  The locking might fail on one of the later
** transitions leaving the lock state different from what it started but
** still short of its goal.  The following chart shows the allowed
** transitions and the inserted intermediate states:
**
**    UNLOCKED -> SHARED
**    SHARED -> RESERVED
**    SHARED -> (PENDING) -> EXCLUSIVE
**    RESERVED -> (PENDING) -> EXCLUSIVE
**    PENDING -> EXCLUSIVE
**
** This routine will only increase a lock.  The sqlite3OsUnlock() routine
** erases all locks at once and returns us immediately to locking level 0.
** It is not possible to lower the locking level one step at a time.  You
** must go straight to locking level 0.
*/
int sqlite3OsLock( OsFile *id, int locktype )
{
#ifdef SQLITE_WCE_OMIT_FILELOCK
	id->locktype = locktype;
	return SQLITE_OK;
#else
  int rc = SQLITE_OK;    /* Return code from subroutines */
  int res = 1;           /* Result of a windows lock call */
  int newLocktype;       /* Set id->locktype to this value before exiting */
  int gotPendingLock = 0;/* True if we acquired a PENDING lock this time */

  assert( id->isOpen );
  TRACE5("LOCK %d %d was %d(%d)\n",
          id->h, locktype, id->locktype, id->sharedLockByte);

  /* If there is already a lock of this type or more restrictive on the
  ** OsFile, do nothing. Don't use the end_lock: exit path, as
  ** sqlite3OsEnterMutex() hasn't been called yet.
  */
  if( id->locktype>=locktype ){
    return SQLITE_OK;
  }

  /* Make sure the locking sequence is correct
  */
  assert( id->locktype!=NO_LOCK || locktype==SHARED_LOCK );
  assert( locktype!=PENDING_LOCK );
  assert( locktype!=RESERVED_LOCK || id->locktype==SHARED_LOCK );

  /* Lock the PENDING_LOCK byte if we need to acquire a PENDING lock or
  ** a SHARED lock.  If we are acquiring a SHARED lock, the acquisition of
  ** the PENDING_LOCK byte is temporary.
  */
  newLocktype = id->locktype;
  if( id->locktype==NO_LOCK
   || (locktype==EXCLUSIVE_LOCK && id->locktype==RESERVED_LOCK)
  ){
    int cnt = 3;
    while( cnt-->0 && (res = getLock(id, PENDING_LOCK))==0 ){
      /* Try 3 times to get the pending lock.  The pending lock might be
      ** held by another reader process who will release it momentarily.
      */
      TRACE2("could not get a PENDING lock. cnt=%d\n", cnt);
      Sleep(1);
    }
    gotPendingLock = res;
  }

  /* Acquire a shared lock
  */
  if( locktype==SHARED_LOCK && res ){
    assert( id->locktype==NO_LOCK );
    res = getLock( id, SHARED_LOCK );
    if( res ){
      newLocktype = SHARED_LOCK;
    }
  }

  /* Acquire a RESERVED lock
  */
  if( locktype==RESERVED_LOCK && res ){
    assert( id->locktype==SHARED_LOCK );
    res = getLock( id, RESERVED_LOCK );
    if( res ){
      newLocktype = RESERVED_LOCK;
    }
  }

  /* Acquire a PENDING lock
  */
  if( locktype==EXCLUSIVE_LOCK && res ){
    newLocktype = PENDING_LOCK;
    gotPendingLock = 0;
  }

  /* Acquire an EXCLUSIVE lock
  */
  if( locktype==EXCLUSIVE_LOCK && res ){
    assert( id->locktype>=SHARED_LOCK );
//  res = unlockReadLock(id);
//  TRACE2("unreadlock = %d\n", res);
    res = getLock( id, EXCLUSIVE_LOCK );
    if( res ){
      newLocktype = EXCLUSIVE_LOCK;
    }else{
      TRACE2("error-code = %d\n", GetLastError());
    }
  }

  /* If we are holding a PENDING lock that ought to be released, then
  ** release it now.
  */
  if( gotPendingLock && locktype==SHARED_LOCK ){
    unsetLock( id, PENDING_LOCK );
  }

  /* Update the state of the lock has held in the file descriptor then
  ** return the appropriate result code.
  */
  if( res ){
    rc = SQLITE_OK;
  }else{
    TRACE4("LOCK FAILED %d trying for %d but got %d\n", id->h,
           locktype, newLocktype);
    rc = SQLITE_BUSY;
  }
  id->locktype = newLocktype;
  return rc;
#endif
}

/*
** This routine checks if there is a RESERVED lock held on the specified
** file by this or any other process. If such a lock is held, return
** non-zero, otherwise zero.
*/
int sqlite3OsCheckReservedLock( OsFile *id )
{
  int rc;
  assert( id->isOpen );
  if( id->locktype>=RESERVED_LOCK ){
    rc = 1;
    TRACE3("TEST WR-LOCK %d %d (local)\n", id->h, rc);
  }else{
#ifdef SQLITE_WCE_OMIT_FILELOCK
	rc = 0;
#else
    /* Only an atomic read, no need to lock_file() */
    rc = ( id->lockdata->lock & (1<<RESERVED_LOCK) ) != 0;
    TRACE3( "TEST WR-LOCK %d %d (remote)\n", id->h, rc );
#endif
  }
  return rc;
}

/*
** Lower the locking level on file descriptor id to locktype.  locktype
** must be either NO_LOCK or SHARED_LOCK.
**
** If the locking level of the file descriptor is already at or below
** the requested locking level, this routine is a no-op.
**
** It is not possible for this routine to fail if the second argument
** is NO_LOCK.  If the second argument is SHARED_LOCK then this routine
** might return SQLITE_IOERR;
*/
int sqlite3OsUnlock( OsFile *id, int locktype )
{
#ifdef SQLITE_WCE_OMIT_FILELOCK
  return SQLITE_OK;
#else
  int type;
  int rc = SQLITE_OK;
  assert( id->isOpen );
  assert( locktype<=SHARED_LOCK );
  TRACE5("UNLOCK %d to %d was %d(%d)\n", id->h, locktype,
          id->locktype, id->sharedLockByte);
  type = id->locktype;
  if( type>=EXCLUSIVE_LOCK ){
	unsetLock( id, EXCLUSIVE_LOCK );
  }
  if( type>=RESERVED_LOCK ){
	unsetLock( id, RESERVED_LOCK );
  }
  if( locktype==NO_LOCK && type>=SHARED_LOCK ){
    unsetLock( id, SHARED_LOCK );
  }
  if( type>=PENDING_LOCK ){
    unsetLock( id, PENDING_LOCK );
  }
  id->locktype = locktype;
  return rc;
#endif
}


#ifndef SQLITE_OMIT_PAGER_PRAGMAS
/*
** Check that a given pathname is a directory and is writable 
**
*/
int sqlite3OsIsDirWritable(char *zBuf){
  int fileAttr;
  if(! zBuf ) return 0;
  fileAttr = GetFileAttributesA(zBuf);
  if( fileAttr == 0xffffffff ) return 0;
  if( (fileAttr & FILE_ATTRIBUTE_DIRECTORY) != FILE_ATTRIBUTE_DIRECTORY ){
    return 0;
  }
  return 1;
}
#endif /* SQLITE_OMIT_PAGER_PRAGMAS */


#endif /* _WIN32_WCE */

}
