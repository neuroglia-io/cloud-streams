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

using CloudStreams.Dashboard.StateManagement;
using System.Reactive.Linq;
using Xunit;

namespace CloudStreams.Dashboard.UnitTests.Cases;

/// <summary>
/// Covers how component stores deliver their states (see #199)
/// </summary>
public sealed class ComponentStoreTests
{

    record TestState(string Resource, string Text);

    sealed class TestStore()
        : ComponentStore<TestState>(new("default", "default"))
    {

        public IObservable<string> Resource => this.Select(s => s.Resource).DistinctUntilChanged();

        public IObservable<string> Text => this.Select(s => s.Text).DistinctUntilChanged();

        public void SetResource(string resource) => this.Reduce(s => s with { Resource = resource });

        public void SetText(string text) => this.Reduce(s => s with { Text = text });

    }

    [Fact]
    public void States_Set_By_Observers_Should_Be_Delivered_After_The_State_They_Observed_And_Never_Be_Overtaken()
    {
        // arrange: an observer derives the text from the resource, as the resource editor does, and is subscribed before the observer of the text
        using var store = new TestStore();
        store.Resource.Subscribe(resource => store.SetText($"text of {resource}"));
        var texts = new List<string>();
        store.Text.Subscribe(texts.Add);

        // act
        store.SetResource("orders");

        // assert: every text is delivered, in order, and the last one is the latest
        Assert.Equal(["text of default", "text of orders"], texts);
    }

    [Fact]
    public void Every_State_Should_Be_Delivered_Synchronously()
    {
        // arrange
        using var store = new TestStore();
        var resources = new List<string>();
        store.Resource.Subscribe(resources.Add);

        // act
        store.SetResource("a");
        store.SetResource("b");
        store.SetResource("c");

        // assert
        Assert.Equal(["default", "a", "b", "c"], resources);
    }

}
