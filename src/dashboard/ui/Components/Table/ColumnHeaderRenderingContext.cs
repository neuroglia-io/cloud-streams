namespace CloudStreams.Dashboard.Components;

/// <summary>
/// Represents a <see cref="ColumnHeader{TData}"/> rendering context
/// </summary>
/// <typeparam name="TData">The type of data</typeparam>
public class ColumnHeaderRenderingContext<TData>
{

    /// <summary>
    /// Initializes a new <see cref="ColumnHeaderRenderingContext{TData}"/>
    /// </summary>
    /// <param name="table">The <see cref="Table{TData}"/> the <see cref="ColumnHeader{TData}"/> belongs to</param>
    /// <param name="column">The <see cref="Column{TData}"/> the <see cref="ColumnHeader{TData}"/> belongs to</param>
    public ColumnHeaderRenderingContext(Table<TData> table, Column<TData> column)
    {
        this.Table = table;
        this.Column = column;
    }

    /// <summary>
    /// Gets the <see cref="Table{TData}"/> the <see cref="ColumnHeader{TData}"/> belongs to
    /// </summary>
    public Table<TData> Table { get; }

    /// <summary>
    /// Gets the <see cref="Column{TData}"/> the <see cref="ColumnHeader{TData}"/> belongs to
    /// </summary>
    public Column<TData> Column { get; }

}
