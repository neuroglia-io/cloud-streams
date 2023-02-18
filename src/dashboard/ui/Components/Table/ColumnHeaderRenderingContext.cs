namespace CloudStreams.Dashboard.Components;

public class ColumnHeaderRenderingContext<TData>
{

    public ColumnHeaderRenderingContext(Table<TData> table, Column<TData> column)
    {
        this.Table = table;
        this.Column = column;
    }

    public Table<TData> Table { get; }

    public Column<TData> Column { get; }

}
