namespace CloudStreams.Dashboard.Components;

/// <summary>
/// Represents a <see cref="Cell{TData}"/> rendering context
/// </summary>
/// <typeparam name="TData">The type of data</typeparam>
public class CellRenderingContext<TData>
{

    /// <summary>
    /// Initializes a new <see cref="CellRenderingContext{TData}"/>
    /// </summary>
    /// <param name="table">The <see cref="Table{TData}"/> the <see cref="Cell{TData}"/> belongs to</param>
    /// <param name="column">The <see cref="Column{TData}"/> the <see cref="Cell{TData}"/> belongs to</param>
    /// <param name="item">The data associated to the <see cref="Cell{TData}"/></param>
    public CellRenderingContext(Table<TData> table, Column<TData> column, TData item)
    {
        this.Table = table;
        this.Column = column;
        this.Item = item;
    }

    /// <summary>
    /// Gets the <see cref="Table{TData}"/> the <see cref="Cell{TData}"/> belongs to
    /// </summary>
    public Table<TData> Table { get; }

    /// <summary>
    /// Gets the <see cref="Column{TData}"/> the <see cref="Cell{TData}"/> belongs to
    /// </summary>
    public Column<TData> Column { get; }

    /// <summary>
    /// Gets the data associated to the <see cref="Cell{TData}"/>
    /// </summary>
    public TData Item { get; }

    private object? _Value;

    /// <summary>
    /// Gets the <see cref="Cell{TData}"/>'s value
    /// </summary>
    public object? Value
    {
        get
        {
            if (this._Value == null)
                this._Value = this.Column.GetValueFor(this.Item);
            return this._Value;
        }
    }

}
