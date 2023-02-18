namespace CloudStreams.Dashboard.Components;

public class CellRenderingContext<TData>
{

    public CellRenderingContext(Table<TData> table, Column<TData> column, TData item)
    {
        this.Table = table;
        this.Column = column;
        this.Item = item;
    }

    public Table<TData> Table { get; }

    public Column<TData> Column { get; }

    public TData Item { get; }

    private object? _Value;

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
