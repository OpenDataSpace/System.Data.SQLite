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
  /// Delegate for receiving row updating events
  /// </summary>
  /// <param name="sender">The SQLiteDataAdapter raising the event</param>
  /// <param name="e">The event's specifics</param>
  public delegate void SQLiteRowUpdatingEventHandler(object sender, RowUpdatingEventArgs e);
  /// <summary>
  /// Delegate for receiving row updated events
  /// </summary>
  /// <param name="sender">The SQLiteDataAdapter raising the event</param>
  /// <param name="e">The event's specifics</param>
  public delegate void SQLiteRowUpdatedEventHandler(object sender, RowUpdatedEventArgs e);

  /// <summary>
  /// SQLite implementation of DbDataAdapter.
  /// </summary>
  public sealed class SQLiteDataAdapter : DbDataAdapter
  {
    private static readonly object RowUpdatingEvent = new object();
    private static readonly object RowUpdatedEvent = new object();

    /// <overloads>
    /// This class is just a shell around the DbDataAdapter.  Nothing from DbDataAdapter is overridden here, just a few constructors are defined.
    /// </overloads>
    /// <summary>
    /// Default constructor.
    /// </summary>
    public SQLiteDataAdapter()
    {
    }

    /// <summary>
    /// Constructs a data adapter using the specified select command.
    /// </summary>
    /// <param name="cmd">The select command to associate with the adapter.</param>
    public SQLiteDataAdapter(SQLiteCommand cmd)
    {
      SelectCommand = cmd;
    }

    /// <summary>
    /// Constructs a data adapter with the supplied select command text and associated with the specified connection.
    /// </summary>
    /// <param name="commandText">The select command text to associate with the data adapter.</param>
    /// <param name="cnn">The connection to associate with the select command.</param>
    public SQLiteDataAdapter(string commandText, SQLiteConnection cnn)
    {
      SelectCommand = new SQLiteCommand(commandText, cnn);
    }

    /// <summary>
    /// Constructs a data adapter with the specified select command text, and using the specified database connection string.
    /// </summary>
    /// <param name="commandText">The select command text to use to construct a select command.</param>
    /// <param name="connectionString">A connection string suitable for passing to a new SQLiteConnection, which is associated with the select command.</param>
    public SQLiteDataAdapter(string commandText, string connectionString)
    {
      SQLiteConnection cnn = new SQLiteConnection(connectionString);
      SelectCommand = new SQLiteCommand(commandText, cnn);
    }

    /// <summary>
    /// Row updating event sink.  Hook your delegate in here
    /// </summary>
    public event SQLiteRowUpdatingEventHandler RowUpdating
    {
      add { base.Events.AddHandler(RowUpdatingEvent, value); }
      remove { base.Events.RemoveHandler(RowUpdatingEvent, value); }
    }

    /// <summary>
    /// Row updated event.  Hook your delegate in here
    /// </summary>
    public event SQLiteRowUpdatedEventHandler RowUpdated
    {
      add { base.Events.AddHandler(RowUpdatedEvent, value); }
      remove { base.Events.RemoveHandler(RowUpdatedEvent, value); }
    }

    /// <summary>
    /// Creates a row updated event object
    /// </summary>
    /// <param name="dataRow">Forwarded to RowUpdatedEventArgs constructor</param>
    /// <param name="command">Forwarded to RowUpdatedEventArgs constructor</param>
    /// <param name="statementType">Forwarded to RowUpdatedEventArgs constructor</param>
    /// <param name="tableMapping">Forwarded to RowUpdatedEventArgs constructor</param>
    /// <returns>A RowUpdatedEventArgs class</returns>
    protected override RowUpdatedEventArgs CreateRowUpdatedEvent(DataRow dataRow, IDbCommand command, StatementType statementType, DataTableMapping tableMapping)
    {
      return new RowUpdatedEventArgs(dataRow, command, statementType, tableMapping);
    }

    /// <summary>
    /// Creates a row updating event object
    /// </summary>
    /// <param name="dataRow">Forwarded to RowUpdatedEventArgs constructor</param>
    /// <param name="command">Forwarded to RowUpdatedEventArgs constructor</param>
    /// <param name="statementType">Forwarded to RowUpdatedEventArgs constructor</param>
    /// <param name="tableMapping">Forwarded to RowUpdatedEventArgs constructor</param>
    /// <returns>A RowUpdatedEventArgs class</returns>
    protected override RowUpdatingEventArgs CreateRowUpdatingEvent(DataRow dataRow, IDbCommand command, StatementType statementType, DataTableMapping tableMapping)
    {
      return new RowUpdatingEventArgs(dataRow, command, statementType, tableMapping);
    }

    /// <summary>
    /// Raised by the underlying DbDataAdapter when a row is being updated
    /// </summary>
    /// <param name="value">The event's specifics</param>
    protected override void OnRowUpdating(RowUpdatingEventArgs value)
    {
      SQLiteRowUpdatingEventHandler eventDelegate = base.Events[RowUpdatingEvent] as SQLiteRowUpdatingEventHandler;
      if (eventDelegate != null)
        eventDelegate(this, value);
    }

    /// <summary>
    /// Raised by DbDataAdapter after a row is updated
    /// </summary>
    /// <param name="value">The event's specifics</param>
    protected override void OnRowUpdated(RowUpdatedEventArgs value)
    {
      SQLiteRowUpdatedEventHandler eventDelegate = base.Events[RowUpdatedEvent] as SQLiteRowUpdatedEventHandler;
      if (eventDelegate != null)
        eventDelegate(this, value);
    }
  }
}
