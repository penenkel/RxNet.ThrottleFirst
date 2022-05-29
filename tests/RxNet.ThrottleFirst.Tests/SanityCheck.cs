using System.Reactive.Linq;

using Microsoft.Reactive.Testing;

using Xunit.Abstractions;

namespace RxNet.ThrottleFirst.Tests
{
    /// <summary>
    /// Apparently the <see cref="MockObserver{T}"/> used by <see cref="TestScheduler"/> does not
    /// quite conform to the usual observer behavior: It does not unsubscribe on completion. Due to
    /// that each observable created by <see cref="TestScheduler"/> must be wrapped with <see
    /// cref="Observable.AsObservable{TSource}(IObservable{TSource})"/> or something similar.
    /// </summary>
    public class SanityCheck : ReactiveTest
    {
        private readonly ITestOutputHelper _output;

        public SanityCheck(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TestableColdObservable_fails_to_unsubscribe_on_completion()
        {
            var scheduler = new TestScheduler();

            var xs = scheduler.CreateColdObservable(
                OnNext(200, 0),
                OnCompleted<int>(300)
           );

            var res = scheduler.Start(() => xs, 0, 50, 550);

            res.Messages.AssertEqual(
                OnNext(250, 0),
                OnCompleted<int>(350)
            );

            xs.Subscriptions.AssertEqual(
                //Subscribe(50, 350)    // this would be the correct behavior
                Subscribe(50, 550)      // but this is what happens
            );
        }

        [Fact]
        public void TestableHotObservable_fails_to_unsubscribe_on_completion()
        {
            var scheduler = new TestScheduler();

            var xs = scheduler.CreateHotObservable(
                OnNext(200, 0),
                OnCompleted<int>(300)
            );

            var res = scheduler.Start(() => xs, 0, 50, 550);

            res.Messages.AssertEqual(
                OnNext(200, 0),
                OnCompleted<int>(300)
            );

            xs.Subscriptions.AssertEqual(
                //Subscribe(50, 300)    // this would be the correct behavior
                Subscribe(50, 550)      // but this is what happens
            );
        }

        [Fact]
        public void Wrapped_TestableColdObservable_unsubscribes_on_completion()
        {
            var scheduler = new TestScheduler();

            var xs = scheduler.CreateColdObservable(
                OnNext(200, 0),
                OnCompleted<int>(300)
           );

            var res = scheduler.Start(() => xs.AsObservable(), 0, 50, 550);

            res.Messages.AssertEqual(
                OnNext(250, 0),
                OnCompleted<int>(350)
            );

            xs.Subscriptions.AssertEqual(
                Subscribe(50, 350)
            );
        }

        [Fact]
        public void Wrapped_TestableHotObservable_unsubscribes_on_completion()
        {
            var scheduler = new TestScheduler();

            var xs = scheduler.CreateHotObservable(
                OnNext(200, 0),
                OnCompleted<int>(300)
            );

            var res = scheduler.Start(() => xs.AsObservable(), 0, 50, 550);

            res.Messages.AssertEqual(
                OnNext(200, 0),
                OnCompleted<int>(300)
            );

            xs.Subscriptions.AssertEqual(
                Subscribe(50, 300)
            );
        }
    }
}