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

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Defines the fundamentals of a service used to provide an implementation of the <see cref="IExpressionEvaluatorProvider"/> interface
/// </summary>
public interface IExpressionEvaluatorProvider
{

    /// <summary>
    /// Gets the provided <see cref="IExpressionEvaluator"/> implementation
    /// </summary>
    /// <returns>An <see cref="IExpressionEvaluator"/> implementation</returns>
    IExpressionEvaluator GetExpressionEvaluator();

}