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
  using System.Collections.Generic;

  /// <summary>
  /// SQLite implementation of DbParameterCollection.
  /// </summary>
  public sealed class SQLiteParameterCollection : DbParameterCollection
  {
    private SQLiteCommand         _command;
    private List<SQLiteParameter> _parameterList;
    private bool                  _unboundFlag;

    internal SQLiteParameterCollection(SQLiteCommand cmd)
    {
      _command = cmd;
      _parameterList = new List<SQLiteParameter>();
      _unboundFlag = true;
    }

    /// <summary>
    /// 
    /// </summary>
    public override bool IsSynchronized
    {
      get { return true; }
    }

    /// <summary>
    /// 
    /// </summary>
    public override bool IsFixedSize
    {
      get { return false; }
    }

    /// <summary>
    /// 
    /// </summary>
    public override bool IsReadOnly
    {
      get { return false; }
    }

    /// <summary>
    /// 
    /// </summary>
    public override object SyncRoot
    {
      get { return null; }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override System.Collections.IEnumerator GetEnumerator()
    {
      return _parameterList.GetEnumerator();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="paramName"></param>
    /// <param name="dbType"></param>
    /// <param name="nSize"></param>
    /// <param name="sourceColumn"></param>
    /// <returns></returns>
    public SQLiteParameter Add(string paramName, DbType dbType, int nSize, string sourceColumn)
    {
      SQLiteParameter param = new SQLiteParameter(paramName, dbType, nSize, sourceColumn);
      Add(param);

      return param;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="paramName"></param>
    /// <param name="dbType"></param>
    /// <param name="nSize"></param>
    /// <returns></returns>
    public SQLiteParameter Add(string paramName, DbType dbType, int nSize)
    {
      SQLiteParameter param = new SQLiteParameter(paramName, dbType, nSize);
      Add(param);

      return param;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="paramName"></param>
    /// <param name="dbType"></param>
    /// <returns></returns>
    public SQLiteParameter Add(string paramName, DbType dbType)
    {
      SQLiteParameter param = new SQLiteParameter(paramName, dbType);
      Add(param);

      return param;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="p"></param>
    /// <returns></returns>
    public int Add(SQLiteParameter p)
    {
      int n = -1;

      if (p.ParameterName != null)
      {
        n = IndexOf(p.ParameterName);
      }

      if (n == -1)
      {
        n = _parameterList.Count;
        _parameterList.Add(p);
      }

      SetParameter(n, p);

      return n;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public override int Add(object value)
    {
      return Add((SQLiteParameter)value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="values"></param>
    public void AddRange(SQLiteParameter[] values)
    {
      int x = values.Length;
      for (int n = 0; n < x; n++)
        Add(values[n]);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="values"></param>
    public override void AddRange(Array values)
    {
      int x = values.Length;
      for (int n = 0; n < x; n++)
        Add((SQLiteParameter)(values.GetValue(n)));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parameterName"></param>
    /// <returns></returns>
    [Obsolete]
    protected override int CheckName(string parameterName)
    {
      throw new NotImplementedException();
    }

    /// <summary>
    /// 
    /// </summary>
    public override void Clear()
    {
      _unboundFlag = true;
      _parameterList.Clear();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public override bool Contains(string value)
    {
      return (IndexOf(value) != -1);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public override bool Contains(object value)
    {
      return _parameterList.Contains((SQLiteParameter)value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="array"></param>
    /// <param name="index"></param>
    public override void CopyTo(Array array, int index)
    {
      throw new NotImplementedException();
    }

    /// <summary>
    /// 
    /// </summary>
    public override int Count
    {
      get { return _parameterList.Count; }
    }

#if !PLATFORM_COMPACTFRAMEWORK
    /// <summary>
    /// 
    /// </summary>
    /// <param name="parameterName"></param>
    /// <returns></returns>
    protected override DbParameter GetParameter(string parameterName)
    {
      return GetParameter(IndexOf(parameterName));
    }
#endif

    /// <summary>
    /// 
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    protected override DbParameter GetParameter(int index)
    {
      return _parameterList[index];
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parameterName"></param>
    /// <returns></returns>
    public override int IndexOf(string parameterName)
    {
      int x = _parameterList.Count;
      for (int n = 0; n < x; n++)
      {
        if (String.Compare(parameterName, _parameterList[n].ParameterName, true) == 0) return n;
      }
      return -1;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public override int IndexOf(object value)
    {
      return _parameterList.IndexOf((SQLiteParameter)value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="index"></param>
    /// <param name="value"></param>
    public override void Insert(int index, object value)
    {
      _unboundFlag = true;
      _parameterList.Insert(index, (SQLiteParameter)value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    public override void Remove(object value)
    {
      _unboundFlag = true;
      _parameterList.Remove((SQLiteParameter)value);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parameterName"></param>
    public override void RemoveAt(string parameterName)
    {
      Remove(IndexOf(parameterName));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="index"></param>
    public override void RemoveAt(int index)
    {
      _unboundFlag = true;
      _parameterList.RemoveAt(index);
    }

#if !PLATFORM_COMPACTFRAMEWORK
    /// <summary>
    /// 
    /// </summary>
    /// <param name="parameterName"></param>
    /// <param name="value"></param>
    protected override void SetParameter(string parameterName, DbParameter value)
    {
      SetParameter(IndexOf(parameterName), value);
    }
#endif

    /// <summary>
    /// 
    /// </summary>
    /// <param name="index"></param>
    /// <param name="value"></param>
    protected override void SetParameter(int index, DbParameter value)
    {
      _unboundFlag = true;
      _parameterList[index] = (SQLiteParameter)value;
    }

    internal void Unbind()
    {
      _unboundFlag = true;
    }

    /// <summary>
    /// This function attempts to map all parameters in the collection to all statements in a Command.
    /// Since named parameters may span multiple statements, this function makes sure all statements are bound
    /// to the same named parameter.  Unnamed parameters are bound in sequence.
    /// </summary>
    internal void MapParameters()
    {
      if (_unboundFlag == false || _parameterList.Count == 0) return;

      int nUnnamed = 0;
      string s;
      int n;
      SQLiteStatement stmt;

      foreach(SQLiteParameter p in _parameterList)
      {
        s = p.ParameterName;
        if (s == null)
        {
          s = String.Format(";{0}", nUnnamed);
          nUnnamed++;
        }

        int x = _command._statementList.Length;
        for (n = 0; n < x; n++)
        {
          stmt = _command._statementList[n];
          if (stmt._paramNames != null)
          {
            stmt.MapParameter(s, p);
          }
        }
      }
      _unboundFlag = false;
    }
  }
}
