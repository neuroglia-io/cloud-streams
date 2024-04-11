// Copyright © 2023-Present The Cloud Streams Authors
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

global using BlazorMonaco.Editor;
global using CloudStreams.Core;
global using CloudStreams.Core.Resources;
global using CloudStreams.Dashboard.Services;
global using CloudStreams.Dashboard.StateManagement;
global using Neuroglia;
global using Neuroglia.Data.Infrastructure.EventSourcing;
global using Neuroglia.Data.Infrastructure.ResourceOriented;
global using Neuroglia.Eventing.CloudEvents;
global using Neuroglia.Reactive;
global using System.Reactive.Linq;