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

namespace CloudStreams.Core.Api.Client.Services;

/// <summary>
/// Defines the fundamentals of the Cloud Streams API dedicated to resource management
/// </summary>
public interface IResourceManagementApiClient
{

    /// <summary>
    /// Gets the API used to manage <see cref="Gateway"/>s
    /// </summary>
    public IResourceManagementApi<Gateway> Gateways { get; }

    /// <summary>
    /// Gets the API used to manage <see cref="Broker"/>s
    /// </summary>
    public IResourceManagementApi<Broker> Brokers { get; }

    /// <summary>
    /// Gets the API used to manage <see cref="Subscription"/>s
    /// </summary>
    public IResourceManagementApi<Subscription> Subscriptions { get; }

}