/********************************************************
 * ADO.NET 2.0 Data Provider for SQLite Version 3.X
 * Written by Robert Simpson (robert@blackcastlesoft.com)
 * 
 * Released to the public domain, use at your own risk!
 ********************************************************/

namespace System.Data.SQLite
{
  using System;
  using System.Runtime.InteropServices;

  /// <summary>
  /// Alternate SQLite3 object, overriding many text behaviors to support UTF-16 (Unicode)
  /// </summary>
  internal class SQLite3_UTF16 : SQLite3
  {
    internal SQLite3_UTF16(DateTimeFormat fmt)
      : base(fmt)
    {
    }

    /// <summary>
    /// Overrides SQLiteConvert.ToString() to marshal UTF-16 strings instead of UTF-8
    /// </summary>
    /// <param name="b">A pointer to a UTF-16 string</param>
    /// <param name="nbytelen">The length (IN BYTES) of the string</param>
    /// <returns>A .NET string</returns>
    public override string ToString(IntPtr b, int nbytelen)
    {
      if (nbytelen == 0) return "";
      return Marshal.PtrToStringUni(b, nbytelen / 2);
    }

    /// <summary>
    /// Another custom string marshaling function
    /// </summary>
    /// <param name="b">A pointer to a zero-terminated UTF-16 string</param>
    /// <returns>A .NET string</returns>
    internal string ToString(IntPtr b)
    {
      if (b == IntPtr.Zero) return "";
      return Marshal.PtrToStringUni(b);
    }

    internal override void Open(string strFilename)
    {
      if (_sql != 0) return;
      int n = UnsafeNativeMethods.sqlite3_open16_interop(strFilename, out _sql);
      if (n > 0) throw new SQLiteException(n, SQLiteLastError());

      _functionsArray = SQLiteFunction.BindFunctions(this);
    }

    internal override string SQLiteLastError()
    {
      return ToString(UnsafeNativeMethods.sqlite3_errmsg16_interop(_sql));
    }

    internal override SQLiteStatement Prepare(string strSql, ref int nParamStart, out string strRemain)
    {
      int stmt;
      IntPtr ptr;

      int n = UnsafeNativeMethods.sqlite3_prepare16_interop(_sql, strSql, strSql.Length, out stmt, out ptr);
      if (n > 0) throw new SQLiteException(n, SQLiteLastError());

      strRemain = ToString(ptr);

      SQLiteStatement cmd = new SQLiteStatement(this, stmt, strSql.Substring(0, strSql.Length - strRemain.Length), ref nParamStart);

      return cmd;
    }

    internal override void Bind_DateTime(SQLiteStatement stmt, int index, DateTime dt)
    {
      Bind_Text(stmt, index, ToString(dt));
    }

    internal override void Bind_Text(SQLiteStatement stmt, int index, string value)
    {
      int n = UnsafeNativeMethods.sqlite3_bind_text16_interop(stmt._sqlite_stmt, index, value, value.Length * 2, -1);
      if (n > 0) throw new SQLiteException(n, SQLiteLastError());
    }

    internal override string ColumnName(SQLiteStatement stmt, int index)
    {
      return ToString(UnsafeNativeMethods.sqlite3_column_name16_interop(stmt._sqlite_stmt, index));
    }

    internal override DateTime GetDateTime(SQLiteStatement stmt, int index)
    {
      return ToDateTime(GetText(stmt, index));
    }
    internal override string GetText(SQLiteStatement stmt, int index)
    {
      return ToString(UnsafeNativeMethods.sqlite3_column_text16_interop(stmt._sqlite_stmt, index));
    }

    internal override string ColumnType(SQLiteStatement stmt, int index, out TypeAffinity nAffinity)
    {
      nAffinity = TypeAffinity.None;

      IntPtr p = UnsafeNativeMethods.sqlite3_column_decltype16_interop(stmt._sqlite_stmt, index);
      if (p != IntPtr.Zero) return ToString(p);
      else
      {
        nAffinity = UnsafeNativeMethods.sqlite3_column_type_interop(stmt._sqlite_stmt, index);
        switch (nAffinity)
        {
          case TypeAffinity.Int64:
            return "BIGINT";
          case TypeAffinity.Double:
            return "DOUBLE";
          case TypeAffinity.Blob:
            return "BLOB";
          default:
            return "TEXT";
        }
      }
    }

    internal override int CreateFunction(string strFunction, int nArgs, SQLiteCallback func, SQLiteCallback funcstep, SQLiteCallback funcfinal)
    {
      int nCookie;

      int n = UnsafeNativeMethods.sqlite3_create_function16_interop(_sql, strFunction, nArgs, 4, func, funcstep, funcfinal, out nCookie);
      if (n > 0) throw new SQLiteException(n, SQLiteLastError());

      return nCookie;
    }

    internal override int CreateCollation(string strCollation, SQLiteCollation func)
    {
      int nCookie;

      int n = UnsafeNativeMethods.sqlite3_create_collation16_interop(_sql, strCollation, 4, 0, func, out nCookie);
      if (n > 0) throw new SQLiteException(n, SQLiteLastError());

      return nCookie;
    }

    internal override string GetParamValueText(int ptr)
    {
      return ToString(UnsafeNativeMethods.sqlite3_value_text16_interop(ptr));
    }

    internal override void ReturnError(int context, string value)
    {
      UnsafeNativeMethods.sqlite3_result_error16_interop(context, value, value.Length);
    }

    internal override void ReturnText(int context, string value)
    {
      UnsafeNativeMethods.sqlite3_result_text16_interop(context, value, value.Length, -1);
    }
  }
}
