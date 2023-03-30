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

using System.Reactive.Linq;

namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="IObservable{T}"/>
/// </summary>
/// <remarks>Original source: <see href="https://github.com/dotnet/reactive/issues/459#issuecomment-357735068">davidnemeti post</see></remarks>
public static class AsyncObservableExtensions
{

    /// <summary>
    /// Subscribes to the specified <see cref="IObservable{T}"/>
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="onNextAsync">Action to invoke for each element in the observable sequence.</param>
    /// <param name="onErrorAsync">Action to invoke upon exceptional termination of the observable sequence.</param>
    /// <param name="onCompletedAsync">Action to invoke upon graceful termination of the observable sequence.</param>
    public static IDisposable SubscribeAsync<T>(this IObservable<T> source, Func<T, Task> onNextAsync, Func<Exception, Task>? onErrorAsync = null, Func<Task>? onCompletedAsync = null)
    {
        onErrorAsync ??= _ => Task.CompletedTask;
        onCompletedAsync ??= () => Task.CompletedTask;
        return source
            .Select(number => Observable.FromAsync(() => onNextAsync(number)))
            .Concat()
            .Subscribe
            (
                _ => { },
                ex => onErrorAsync(ex).GetAwaiter().GetResult(),
                () => onCompletedAsync().GetAwaiter().GetResult()
            );
    }

    /// <summary>
    /// Subscribes to the specified <see cref="IObservable{T}"/>
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="onNextAsync">Action to invoke for each element in the observable sequence.</param>
    /// <param name="onErrorAsync">Action to invoke upon exceptional termination of the observable sequence.</param>
    /// <param name="onCompletedAsync">Action to invoke upon graceful termination of the observable sequence.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    public static void SubscribeAsync<T>(this IObservable<T> source, Func<T, Task> onNextAsync, Func<Exception, Task>? onErrorAsync, Func<Task>? onCompletedAsync, CancellationToken cancellationToken = default)
    {
        onErrorAsync ??= _ => Task.CompletedTask;
        onCompletedAsync ??= () => Task.CompletedTask;
        source
            .Select(number => Observable.FromAsync(() => onNextAsync(number)))
            .Concat()
            .Subscribe
            (
                _ => { },
                ex => onErrorAsync(ex).GetAwaiter().GetResult(),
                () => onCompletedAsync().GetAwaiter().GetResult(),
                cancellationToken
            );
    }

    /// <summary>
    /// Subscribes to the specified <see cref="IObservable{T}"/>
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="onNextAsync">Action to invoke for each element in the observable sequence.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    public static void Subscribe<T>(this IObservable<T> source, Func<T, Task> onNextAsync, CancellationToken cancellationToken)
    {
        source
            .Select(number => Observable.FromAsync(() => onNextAsync(number)))
            .Concat()
            .Subscribe(cancellationToken);
    }

    /// <summary>
    /// Subscribes to the specified <see cref="IObservable{T}"/> concurrently
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="onNextAsync">Action to invoke for each element in the observable sequence.</param>
    /// <returns><see cref="IDisposable"/> object used to unsubscribe from the observable sequence.</returns>
    public static IDisposable SubscribeAsyncConcurrent<T>(this IObservable<T> source, Func<T, Task> onNextAsync)
    {
        return source
            .Select(number => Observable.FromAsync(() => onNextAsync(number)))
            .Merge()
            .Subscribe();
    }

    /// <summary>
    /// Subscribes to the specified <see cref="IObservable{T}"/> concurrently
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="onNextAsync">Action to invoke for each element in the observable sequence.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    public static void SubscribeAsyncConcurrent<T>(this IObservable<T> source, Func<T, Task> onNextAsync, CancellationToken cancellationToken)
    {
        source
            .Select(number => Observable.FromAsync(() => onNextAsync(number)))
            .Merge()
            .Subscribe(cancellationToken);
    }

    /// <summary>
    /// Subscribes to the specified <see cref="IObservable{T}"/> concurrently
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="onNextAsync">Action to invoke for each element in the observable sequence.</param>
    /// <param name="maxConcurrent">The maximum amount of concurrent threads.</param>
    /// <returns><see cref="IDisposable"/> object used to unsubscribe from the observable sequence.</returns>
    public static IDisposable SubscribeAsyncConcurrent<T>(this IObservable<T> source, Func<T, Task> onNextAsync, int maxConcurrent)
    {
        return Observable.Merge(source
            .Select(number => Observable.FromAsync(() => onNextAsync(number)))
, maxConcurrent)
            .Subscribe();
    }

    /// <summary>
    /// Subscribes to the specified <see cref="IObservable{T}"/> concurrently
    /// </summary>
    /// <typeparam name="T">The type of the elements in the source sequence.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="onNextAsync">Action to invoke for each element in the observable sequence.</param>
    /// <param name="maxConcurrent">The maximum amount of concurrent threads.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    public static void SubscribeAsyncConcurrent<T>(this IObservable<T> source, Func<T, Task> onNextAsync, int maxConcurrent, CancellationToken cancellationToken)
    {
        Observable.Merge(source
            .Select(number => Observable.FromAsync(() => onNextAsync(number)))
, maxConcurrent)
            .Subscribe(cancellationToken);
    }

}
