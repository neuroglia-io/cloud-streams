namespace CloudStreams.Dashboard.Components;

/// <summary>
/// Represents a <see cref="Row{TData}"/> rendering context
/// </summary>
/// <typeparam name="TData">The type of data</typeparam>
public class RowRenderingContext<TData>
{

    /// <summary>
    /// Initializes a new <see cref="RowRenderingContext{TData}"/>
    /// </summary>
    /// <param name="table">The <see cref="Table{TData}"/> the <see cref="Row{TData}"/> belongs to</param>
    /// <param name="item"></param>
    /// <param name="index"></param>
    public RowRenderingContext(Table<TData> table, TData item, int index)
    {
        this.Table = table;
        this.Item = item;
        this.Index = index;
    }

    /// <summary>
    /// Gets the <see cref="Table{TData}"/> the <see cref="Row{TData}"/> belongs to
    /// </summary>
    public Table<TData> Table { get; }

    /// <summary>
    /// Gets the data associated with the <see cref="Row{TData}"/>
    /// </summary>
    public TData Item { get; }

    /// <summary>
    /// Gets the <see cref="Row{TData}"/>'s index
    /// </summary>
    public int Index { get; }

}
