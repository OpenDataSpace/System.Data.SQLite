/********************************************************
 * ADO.NET 2.0 Data Provider for SQLite Version 3.X
 * Written by Robert Simpson (robert@blackcastlesoft.com)
 * 
 * Released to the public domain, use at your own risk!
 ********************************************************/

namespace System.Data.SQLite
{
  using System;
  using System.Collections.Generic;
  using System.Text;
  using System.Data.Common;

#if !PLATFORM_COMPACTFRAMEWORK
  using System.Runtime.Serialization;
#endif

  /// <summary>
  /// SQLite exception class.
  /// </summary>
#if !PLATFORM_COMPACTFRAMEWORK
  [Serializable]
#endif
  public sealed class SQLiteException : Exception
  {
    internal SQLiteException(int nCode, string strMessage) : base(Initialize(nCode, strMessage))
    {
      HResult = (int)((uint)0x800F0000 | (uint)nCode);
    }

#if !PLATFORM_COMPACTFRAMEWORK

    private SQLiteException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
#endif

    /// <summary>
    /// Various public constructors that just pass along to the base DbException
    /// </summary>
    /// <param name="message">Passed verbatim to DbException</param>
    public SQLiteException(string message)
      : base(message)
    {
    }

    /// <summary>
    /// Various public constructors that just pass along to the base DbException
    /// </summary>
    public SQLiteException()
    {
    }

    /// <summary>
    /// Various public constructors that just pass along to the base DbException
    /// <param name="message">Passed to DbException</param>
    /// <param name="innerException">Passed to DbException</param>
    /// </summary>
    public SQLiteException(string message, Exception innerException)
      : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes the exception class with the SQLite error code.
    /// </summary>
    /// <param name="nCode">The SQLite error code</param>
    /// <param name="strMessage">A detailed error message</param>
    /// <returns>An error message string</returns>
    /// <remarks>
    /// The SQLite error code is OR'd with 0x800F0000 to generate an HResult
    /// </remarks>
    private static string Initialize(int nCode, string strMessage)
    {
      if (strMessage == null) strMessage = "";

      if (strMessage.Length > 0)
        strMessage = "\r\n" + strMessage;

      switch (nCode)
      {
        case 1:
          return "SQLite error" + strMessage;
        case 2:
          return "An internal logic error in SQLite" + strMessage;
        case 3:
          return "Access permission denied" + strMessage;
        case 4:
          return "Callback routine requested an abort" + strMessage;
        case 5:
          return "The database file is locked" + strMessage;
        case 6:
          return "A table in the database is locked" + strMessage;
        case 7:
          return "A malloc() failed" + strMessage;
        case 8:
          return "Attempt to write a readonly database" + strMessage;
        case 9:
          return "Operation terminated by sqlite3_interrupt()" + strMessage;
        case 10:
          return "Some kind of disk I/O error occurred" + strMessage;
        case 11:
          return "The database disk image is malformed" + strMessage;
        case 12:
          return "Table or record not found" + strMessage;
        case 13:
          return "Insertion failed because database is full" + strMessage;
        case 14:
          return "Unable to open the database file" + strMessage;
        case 15:
          return "Database lock protocol error" + strMessage;
        case 16:
          return "Database is empty" + strMessage;
        case 17:
          return "The database schema changed" + strMessage;
        case 18:
          return "Too much data for one row of a table" + strMessage;
        case 19:
          return "Abort due to constraint violation" + strMessage;
        case 20:
          return "Data type mismatch" + strMessage;
        case 21:
          return "Library used incorrectly" + strMessage;
        case 22:
          return "Uses OS features not supported on host" + strMessage;
        case 23:
          return "Authorization denied" + strMessage;
        case 24:
          return "Auxiliary database format error" + strMessage;
        case 25:
          return "2nd parameter to sqlite3_bind() out of range" + strMessage;
        case 26:
          return "File opened that is not a database file" + strMessage;
      }
      return strMessage;
    }
  }
}
