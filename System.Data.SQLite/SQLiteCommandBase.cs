using System;

namespace System.Data.SQLite
{
  internal sealed class SQLiteCommandBase : IDisposable
  {
    internal SQLiteBase _sqlbase;
    internal string     _strCommand;
    internal int        _sqlite_stmt;

    internal SQLiteCommandBase(SQLiteBase sqlbase, int stmt, string strCommand)
    {
      _sqlbase     = sqlbase;
      _sqlite_stmt = stmt;
      _strCommand  = strCommand;
    }

    #region IDisposable Members

    public void Dispose()
    {
      _sqlbase.Finalize(this);
    }

    #endregion
  }
}
