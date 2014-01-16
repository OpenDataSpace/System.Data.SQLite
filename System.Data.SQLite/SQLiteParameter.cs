/********************************************************
 * ADO.NET 2.0 Data Provider for SQLite Version 3.X
 * Written by Robert Simpson (robert@blackcastlesoft.com)
 * 
 * Released to the public domain, use at your own risk!
 ********************************************************/

namespace System.Data.SQLite
{
  using System;
  using System.Data;
  using System.Data.Common;

  /// <summary>
  /// SQLite implementation of DbParameter.
  /// </summary>
  public sealed class SQLiteParameter : DbParameter
  {
    private int            _dbType;
    private DataRowVersion _rowVersion;
    private Object         _objValue;
    private string         _sourceColumn;
    private string         _columnName;
    private int            _dataSize;

    /// <summary>
    /// 
    /// </summary>
    public SQLiteParameter()
    {
      Initialize(null, -1, 0, null, DataRowVersion.Current);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parameterName"></param>
    public SQLiteParameter(string parameterName)
    {
      Initialize(parameterName, -1, 0, null, DataRowVersion.Current);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parameterName"></param>
    /// <param name="dbType"></param>
    public SQLiteParameter(string parameterName, DbType dbType)
    {
      Initialize(parameterName, (int)dbType, 0, null, DataRowVersion.Current);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parameterName"></param>
    /// <param name="dbType"></param>
    /// <param name="sourceColumn"></param>
    public SQLiteParameter(string parameterName, DbType dbType, string sourceColumn)
    {
      Initialize(parameterName, (int)dbType, 0, sourceColumn, DataRowVersion.Current);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parameterName"></param>
    /// <param name="dbType"></param>
    /// <param name="sourceColumn"></param>
    /// <param name="rowVersion"></param>
    public SQLiteParameter(string parameterName, DbType dbType, string sourceColumn, DataRowVersion rowVersion)
    {
      Initialize(parameterName, (int)dbType, 0, sourceColumn, rowVersion);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dbType"></param>
    public SQLiteParameter(DbType dbType)
    {
      Initialize(null, (int)dbType, 0, null, DataRowVersion.Current);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dbType"></param>
    /// <param name="sourceColumn"></param>
    public SQLiteParameter(DbType dbType, string sourceColumn)
    {
      Initialize(null, (int)dbType, 0, sourceColumn, DataRowVersion.Current);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dbType"></param>
    /// <param name="sourceColumn"></param>
    /// <param name="rowVersion"></param>
    public SQLiteParameter(DbType dbType, string sourceColumn, DataRowVersion rowVersion)
    {
      Initialize(null, (int)dbType, 0, sourceColumn, rowVersion);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parameterName"></param>
    /// <param name="dbType"></param>
    /// <param name="nSize"></param>
    public SQLiteParameter(string parameterName, DbType dbType, int nSize)
    {
      Initialize(parameterName, (int)dbType, nSize, null, DataRowVersion.Current);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parameterName"></param>
    /// <param name="dbType"></param>
    /// <param name="nSize"></param>
    /// <param name="sourceColumn"></param>
    public SQLiteParameter(string parameterName, DbType dbType, int nSize, string sourceColumn)
    {
      Initialize(parameterName, (int)dbType, nSize, sourceColumn, DataRowVersion.Current);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parameterName"></param>
    /// <param name="dbType"></param>
    /// <param name="nSize"></param>
    /// <param name="sourceColumn"></param>
    /// <param name="rowVersion"></param>
    public SQLiteParameter(string parameterName, DbType dbType, int nSize, string sourceColumn, DataRowVersion rowVersion)
    {
      Initialize(parameterName, (int)dbType, nSize, sourceColumn, rowVersion);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dbType"></param>
    /// <param name="nSize"></param>
    public SQLiteParameter(DbType dbType, int nSize)
    {
      Initialize(null, (int)dbType, nSize, null, DataRowVersion.Current);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dbType"></param>
    /// <param name="nSize"></param>
    /// <param name="sourceColumn"></param>
    public SQLiteParameter(DbType dbType, int nSize, string sourceColumn)
    {
      Initialize(null, (int)dbType, nSize, sourceColumn, DataRowVersion.Current);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dbType"></param>
    /// <param name="nSize"></param>
    /// <param name="sourceColumn"></param>
    /// <param name="rowVersion"></param>
    public SQLiteParameter(DbType dbType, int nSize, string sourceColumn, DataRowVersion rowVersion)
    {
      Initialize(null, (int)dbType, nSize, sourceColumn, rowVersion);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parameterName"></param>
    /// <param name="dbType"></param>
    /// <param name="nSize"></param>
    /// <param name="sourceColumn"></param>
    /// <param name="rowVersion"></param>
    private void Initialize(string parameterName, int dbType, int nSize, string sourceColumn, DataRowVersion rowVersion)
    {
      _columnName = parameterName;
      _dbType = dbType;
      _sourceColumn = sourceColumn;
      _rowVersion = rowVersion;
      _objValue = null;
      _dataSize = nSize;
    }

    /// <summary>
    /// 
    /// </summary>
    public override bool IsNullable
    {
      get
      {
        return true;
      }
      set 
      {
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="destination"></param>
    [Obsolete]
    public override void CopyTo(DbParameter destination)
    {
      throw new NotImplementedException();
    }

    /// <summary>
    /// 
    /// </summary>
    public override DbType DbType
    {
      get
      {
        if (_dbType == -1) return DbType.String; // Unassigned default value is String
        return (DbType)_dbType;
      }
      set
      {
        _dbType = (int)value;
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public override ParameterDirection Direction
    {
      get
      {
        return ParameterDirection.Input;
      }
      set
      {
        if (value != ParameterDirection.Input)
          throw new NotImplementedException();
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public override int Offset
    {
      get
      {
        throw new NotImplementedException();
      }
      set
      {
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public override string ParameterName
    {
      get
      {
        return _columnName;
      }
      set
      {
        _columnName = value;
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public override void ResetDbType()
    {
      throw new NotImplementedException();
    }

    /// <summary>
    /// 
    /// </summary>
    public override int Size
    {
      get
      {
        return _dataSize;
      }
      set
      {
        _dataSize = value;
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public override string SourceColumn
    {
      get
      {
        return _sourceColumn;
      }
      set
      {
        _sourceColumn = value;
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public override bool SourceColumnNullMapping
    {
      get
      {
        return false;
      }
      set
      {
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public override DataRowVersion SourceVersion
    {
      get
      {
        return _rowVersion;
      }
      set
      {
        _rowVersion = value;
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public override object Value
    {
      get
      {
        return _objValue;
      }
      set
      {
        _objValue = value;
        if (_dbType == -1 && _objValue != null && _objValue != DBNull.Value) // If the DbType has never been assigned, try to glean one from the value's datatype 
          _dbType = (int)SQLiteConvert.TypeToDbType(_objValue.GetType());
      }
    }    
  }
}
