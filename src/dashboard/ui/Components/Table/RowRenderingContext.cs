namespace CloudStreams.Dashboard.Components;

public class RowRenderingContext<TData>
{

    public RowRenderingContext(Table<TData> table, TData item, int index)
    {
        this.Table = table;
        this.Item = item;
        this.Index = index;
    }

    public Table<TData> Table { get; }

    public TData Item { get; }

    public int Index { get; }

}
