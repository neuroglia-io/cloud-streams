﻿// Copyright © 2024-Present The Cloud Streams Authors
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

using System.ComponentModel;

namespace CloudStreams.Dashboard.Components;

/// <summary>
/// Defines the fundamentals of a service used to manage the application's layout
/// </summary>
public interface IApplicationLayout
    : INotifyPropertyChanged
{

    /// <summary>
    /// Gets the application's current title, which is aggregated to produce the current page's title
    /// </summary>
    ApplicationTitle? Title { get; set; }

    /// <summary>
    /// Handles changes in the application's title
    /// </summary>
    void OnTitleChanged();

}
