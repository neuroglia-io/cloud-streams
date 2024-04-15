// Copyright © 2024-Present The Cloud Streams Authors
//
// Licensed under the Apache License, Version 2.0 (the "License"),
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
