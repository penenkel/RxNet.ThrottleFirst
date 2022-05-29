using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;

using Microsoft.Reactive.Testing;

namespace RxNet.ThrottleFirst.Tests;

public class ThrottleFirstTest : ReactiveTest
{
    [Fact]
    public void Constant_ArgumentChecking()
    {
        var scheduler = new TestScheduler();
        var someObservable = Observable.Empty<int>();

        ReactiveAssert.Throws<ArgumentNullException>(() => ThrottleFirstObservableExtensions.ThrottleFirst((IObservable<int>)null!, TimeSpan.Zero));
        ReactiveAssert.Throws<ArgumentNullException>(() => ThrottleFirstObservableExtensions.ThrottleFirst(someObservable, TimeSpan.Zero, null!));
        ReactiveAssert.Throws<ArgumentNullException>(() => ThrottleFirstObservableExtensions.ThrottleFirst((IObservable<int>)null!, TimeSpan.Zero, scheduler));

        ReactiveAssert.Throws<ArgumentOutOfRangeException>(() => ThrottleFirstObservableExtensions.ThrottleFirst(someObservable, TimeSpan.FromSeconds(-1)));
        ReactiveAssert.Throws<ArgumentOutOfRangeException>(() => ThrottleFirstObservableExtensions.ThrottleFirst(someObservable, TimeSpan.FromSeconds(-1), scheduler));
    }

    [Fact]
    public void Constant_Behavior()
    {
        var scheduler = new TestScheduler();

        var xs = scheduler.CreateHotObservable(
            OnNext(100, 0), // publish
            OnNext(200, 1), // skip, because timeout has not yet expired
            OnNext(300, 2), // publish
            OnNext(400, 3), // skip, because timeout has not yet expired
            OnCompleted<int>(500) // publish
        );

        var res = scheduler.Start(() =>
            xs.ThrottleFirst(TimeSpan.FromTicks(150), scheduler),
            0, 50, 550
        );

        res.Messages.AssertEqual(
            OnNext(100, 0),
            OnNext(300, 2),
            OnCompleted<int>(500)
        );

        xs.Subscriptions.AssertEqual(
            Subscribe(50, 500)
        );
    }

    [Fact]
    public void Constant_DefaultScheduler()
    {
        Assert.Equal(Observable.Return(1).ThrottleFirst(TimeSpan.FromMilliseconds(1)).ToEnumerable(), new[] { 1 });
    }

    [Fact]
    public void Dynamic_ArgumentChecking()
    {
        var someObservable = DummyObservable<int>.Instance;

        ReactiveAssert.Throws<ArgumentNullException>(() => ThrottleFirstObservableExtensions.ThrottleFirst((IObservable<int>)null!, x => someObservable));
        ReactiveAssert.Throws<ArgumentNullException>(() => ThrottleFirstObservableExtensions.ThrottleFirst(someObservable, (Func<int, IObservable<string>>)null!));
    }

    [Fact]
    public void Dynamic_Behavior()
    {
        var scheduler = new TestScheduler();

        var xs = scheduler.CreateHotObservable(
            OnNext(100, 0), // publish
            OnNext(200, 1), // skip, because timeout has not yet expired
            OnNext(300, 2), // publish
            OnNext(400, 3), // skip, because timeout has not yet expired
            OnCompleted<int>(500) // publish
        );

        var ys = scheduler.CreateColdObservable(
            OnNext(150, 42),
            OnCompleted<int>(150)
        );

        var res = scheduler.Start(() =>
            xs.ThrottleFirst(x => ys.AsObservable()),
            0, 50, 550
        );

        res.Messages.AssertEqual(
            OnNext(100, 0),
            OnNext(300, 2),
            OnCompleted<int>(500)
        );

        xs.Subscriptions.AssertEqual(
            Subscribe(50, 500)
        );

        ys.Subscriptions.AssertEqual(
            Subscribe(100, 250),
            Subscribe(300, 450)
        );
    }

    [Fact]
    public void Dynamic_InnerCompletingAfterOuter()
    {
        var scheduler = new TestScheduler();

        var xs = scheduler.CreateHotObservable(
            OnNext(100, 0), // publish
            OnNext(200, 1), // skip, because timeout has not yet expired
            OnNext(300, 2), // skip, because timeout has not yet expired
            OnNext(400, 3), // skip, because timeout has not yet expired
            OnCompleted<int>(500) // publish
        );

        var ys = scheduler.CreateColdObservable(
            OnNext(425, 42),
            OnCompleted<int>(425)
        );

        var res = scheduler.Start(() =>
            xs.ThrottleFirst(x => ys.AsObservable()),
            0, 50, 550
        );

        res.Messages.AssertEqual(
            OnNext(100, 0),
            OnCompleted<int>(500)
        );

        xs.Subscriptions.AssertEqual(
            Subscribe(50, 500)
        );

        ys.Subscriptions.AssertEqual(
            Subscribe(100, 500) // ends early, simultaneously with outer
        );
    }

    [Fact]
    public void Dynamic_InnerEmpty()
    {
        var scheduler = new TestScheduler();

        var xs = scheduler.CreateHotObservable(
            OnNext(100, 0), // publish
            OnNext(200, 1), // skip, because timeout has not yet expired
            OnNext(300, 2), // publish
            OnNext(400, 3), // skip, because timeout has not yet expired
            OnCompleted<int>(500) // publish
        );

        var ys = scheduler.CreateColdObservable(
            OnCompleted<int>(150)
        );

        var res = scheduler.Start(() =>
            xs.ThrottleFirst(x => ys.AsObservable()),
            0, 50, 550
        );

        res.Messages.AssertEqual(
            OnNext(100, 0),
            OnNext(300, 2),
            OnCompleted<int>(500)
        );

        xs.Subscriptions.AssertEqual(
            Subscribe(50, 500)
        );

        ys.Subscriptions.AssertEqual(
            Subscribe(100, 250),
            Subscribe(300, 450)
        );
    }

    [Fact]
    public void Dynamic_InnerEmptyWithoutCompletion()
    {
        var scheduler = new TestScheduler();

        var xs = scheduler.CreateHotObservable(
            OnNext(100, 0), // publish
            OnNext(200, 1), // skip, because timeout has not yet expired
            OnNext(300, 2), // skip, because timeout has not yet expired
            OnNext(400, 3), // skip, because timeout has not yet expired
            OnCompleted<int>(500) // publish
        );

        var ys = scheduler.CreateColdObservable(
            Array.Empty<Recorded<Notification<int>>>()
        );

        var res = scheduler.Start(() =>
            xs.ThrottleFirst(x => ys.AsObservable()),
            0, 50, 550
        );

        res.Messages.AssertEqual(
            OnNext(100, 0),
            OnCompleted<int>(500)
        );

        xs.Subscriptions.AssertEqual(
            Subscribe(50, 500)
        );

        ys.Subscriptions.AssertEqual(
            Subscribe(100, 500) // ends simultaneously with outer
        );
    }

    [Fact]
    public void Dynamic_InnerError()
    {
        var exception = new Exception();
        var scheduler = new TestScheduler();

        var xs = scheduler.CreateHotObservable(
            OnNext(100, 0), // publish
            OnNext(200, 1), // skip, due to error
            OnNext(300, 2), // skip, due to error
            OnNext(400, 1), // skip, due to error
            OnCompleted<int>(500) // skip, due to error
        );

        var ys = scheduler.CreateColdObservable(
            OnError<int>(50, exception)
        );

        var res = scheduler.Start(() =>
            xs.ThrottleFirst(x => ys.AsObservable()),
            0, 50, 550
        );

        res.Messages.AssertEqual(
            OnNext(100, 0),
            OnError<int>(150, exception)
        );

        xs.Subscriptions.AssertEqual(
            Subscribe(50, 150) // ends with error
        );
    }

    [Fact]
    public void Dynamic_InnerWithoutCompletion()
    {
        var scheduler = new TestScheduler();

        var xs = scheduler.CreateHotObservable(
            OnNext(100, 0), // publish
            OnNext(200, 1), // skip, because timeout has not yet expired
            OnNext(300, 2), // publish
            OnNext(400, 3), // skip, because timeout has not yet expired
            OnCompleted<int>(500) // publish
        );

        var ys = scheduler.CreateColdObservable(
            OnNext(150, 42)
        );

        var res = scheduler.Start(() =>
            xs.ThrottleFirst(x => ys.AsObservable()),
            0, 50, 550
        );

        res.Messages.AssertEqual(
            OnNext(100, 0),
            OnNext(300, 2),
            OnCompleted<int>(500)
        );

        xs.Subscriptions.AssertEqual(
            Subscribe(50, 500)
        );

        ys.Subscriptions.AssertEqual(
            Subscribe(100, 250),
            Subscribe(300, 450)
        );
    }

    [Fact]
    public void Dynamic_OuterError()
    {
        var scheduler = new TestScheduler();
        Exception exception = new Exception();

        var xs = scheduler.CreateHotObservable(
            OnNext(100, 0), // publish
            OnNext(200, 1), // skip, because timeout has not yet expired
            OnError<int>(300, exception), // publish
            OnNext(400, 3), // skip, due to error
            OnCompleted<int>(500) // skip, due to error
        );

        var ys = scheduler.CreateColdObservable(
            OnNext(150, 42),
            OnCompleted<int>(150)
        );

        var res = scheduler.Start(() =>
            xs.ThrottleFirst(x => ys.AsObservable()),
            0, 50, 550
        );

        res.Messages.AssertEqual(
            OnNext(100, 0),
            OnError<int>(300, exception)
        );

        xs.Subscriptions.AssertEqual(
            Subscribe(50, 300)
        );

        ys.Subscriptions.AssertEqual(
            Subscribe(100, 250)
        );
    }

    [Fact]
    public void Dynamic_OuterErrorBeforeInnerEmits()
    {
        var scheduler = new TestScheduler();
        Exception exception = new Exception();

        var xs = scheduler.CreateHotObservable(
            OnNext(100, 0), // publish
            OnNext(200, 1), // skip, because timeout has not yet expired
            OnError<int>(300, exception), // publish, even though timeout has not yet expired
            OnNext(400, 3), // skip, due to error
            OnCompleted<int>(500) // skip, due to error
        );

        var ys = scheduler.CreateColdObservable(
            OnNext(250, 42),
            OnCompleted<int>(250)
        );

        var res = scheduler.Start(() =>
            xs.ThrottleFirst(x => ys.AsObservable()),
            0, 50, 550
        );

        res.Messages.AssertEqual(
            OnNext(100, 0),
            OnError<int>(300, exception)
        );

        xs.Subscriptions.AssertEqual(
            Subscribe(50, 300)
        );

        ys.Subscriptions.AssertEqual(
            Subscribe(100, 300) // ends early, simultaneously with outer error
        );
    }

    [Fact]
    public void SanityCheck_ColdObservable_can_be_subscribed_multiple_times()
    {
        var scheduler = new TestScheduler();

        var xs = scheduler.CreateColdObservable(
            OnNext(200, 0),
            OnCompleted<int>(300)
        );

        var obs1 = scheduler.CreateObserver<int>();
        var obs2 = scheduler.CreateObserver<int>();
        IDisposable? s1 = null;
        IDisposable? s2 = null;

        scheduler.ScheduleAbsolute<int>(default, 50, (s, state) => s1 = xs.AsObservable().Subscribe(obs1));
        scheduler.ScheduleAbsolute<int>(default, 150, (s, state) => s2 = xs.AsObservable().Subscribe(obs2));

        scheduler.ScheduleAbsolute<int>(default, 550, (s, state) => { s1?.Dispose(); return Disposable.Empty; });
        scheduler.ScheduleAbsolute<int>(default, 550, (s, state) => { s2?.Dispose(); return Disposable.Empty; });

        scheduler.Start();

        xs.Subscriptions.AssertEqual(
            Subscribe(50, 350),
            Subscribe(150, 450)
        );
    }
}