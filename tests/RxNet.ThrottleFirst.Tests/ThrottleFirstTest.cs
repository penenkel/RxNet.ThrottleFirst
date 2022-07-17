using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;

using Microsoft.Reactive.Testing;

using NFluent;

namespace RxNet.ThrottleFirst.Tests;

public class ThrottleFirstTest : ReactiveTest
{
    private static readonly TimeSpan NegativTimeSpan = TimeSpan.FromSeconds(-1);
    private static readonly IObservable<int> NullObservable = null!;
    private static readonly IScheduler NullScheduler = null!;
    private static readonly string SchedulerNullExceptionMessage = "Value cannot be null. (Parameter 'scheduler')";
    private static readonly IObservable<int> SomeObservable = Observable.Empty<int>();
    private static readonly IScheduler SomeScheduler = new TestScheduler();
    private static readonly TimeSpan SomeTimeSpan = TimeSpan.Zero;
    private static readonly string SourceNullExceptionMessage = "Value cannot be null. (Parameter 'source')";
    private static readonly string SuppressionPeriodNegativeExceptionMessage = "Cannot be negative. (Parameter 'suppressionPeriod')";
    private static readonly string SuppressionPeriodSelectorNullExceptionMessage = "Value cannot be null. (Parameter 'suppressionPeriodSelector')";
    private readonly Func<int, IObservable<string>> NullSuppressionPeriodSelector = null!;
    private readonly Func<int, IObservable<string>> SomeSuppressionPeriodSelector = _ => DummyObservable<string>.Instance;

    [Fact]
    public void Constant_ArgumentChecking()
    {
        Check.ThatCode(() => ThrottleFirstObservableExtensions.ThrottleFirst(NullObservable!, SomeTimeSpan))
            .Throws<ArgumentNullException>().WithMessage(SourceNullExceptionMessage);
        Check.ThatCode(() => ThrottleFirstObservableExtensions.ThrottleFirst(SomeObservable, NegativTimeSpan))
            .Throws<ArgumentOutOfRangeException>().WithMessage(SuppressionPeriodNegativeExceptionMessage);
    }

    [Fact]
    public void Constant_ArgumentChecking_WithScheduler()
    {
        Check.ThatCode(() => ThrottleFirstObservableExtensions.ThrottleFirst(NullObservable!, SomeTimeSpan, SomeScheduler))
            .Throws<ArgumentNullException>().WithMessage(SourceNullExceptionMessage);
        Check.ThatCode(() => ThrottleFirstObservableExtensions.ThrottleFirst(SomeObservable, SomeTimeSpan, NullScheduler!))
            .Throws<ArgumentNullException>().WithMessage(SchedulerNullExceptionMessage);
        Check.ThatCode(() => ThrottleFirstObservableExtensions.ThrottleFirst(SomeObservable, NegativTimeSpan, SomeScheduler))
            .Throws<ArgumentOutOfRangeException>().WithMessage(SuppressionPeriodNegativeExceptionMessage);
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
        Check.ThatCode(() => ThrottleFirstObservableExtensions.ThrottleFirst(NullObservable!, SomeSuppressionPeriodSelector))
            .Throws<ArgumentNullException>().WithMessage(SourceNullExceptionMessage);
        Check.ThatCode(() => ThrottleFirstObservableExtensions.ThrottleFirst(SomeObservable, NullSuppressionPeriodSelector))
            .Throws<ArgumentNullException>().WithMessage(SuppressionPeriodSelectorNullExceptionMessage);
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
    public void Dynamic_InnerEmpty_InnerCompletingAfterOuter()
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
    public void Dynamic_InnerEmpty_InnerWithoutCompletion()
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
        Exception exception = new();

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
        Exception exception = new();

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
    public void Dynamic_OuterUnsubscribedEarly()
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
            0, 50, 250 // unsubscribe early
        );

        res.Messages.AssertEqual(
            OnNext(100, 0)
        );

        xs.Subscriptions.AssertEqual(
            Subscribe(50, 250) // ends early, due to outer being explicitly unsubscripted
        );

        ys.Subscriptions.AssertEqual(
            Subscribe(100, 250) // ends early, due to outer being explicitly unsubscripted
        );
    }

    [Fact]
    public void Dynamic_OuterUnsubscribedEarly_InnerCompletingAfterOuter()
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
            OnNext(425, 42),
            OnCompleted<int>(425)
        );

        var res = scheduler.Start(() =>
            xs.ThrottleFirst(x => ys.AsObservable()),
            0, 50, 250 // unsubscribe early
        );

        res.Messages.AssertEqual(
            OnNext(100, 0)
        );

        xs.Subscriptions.AssertEqual(
            Subscribe(50, 250) // ends early, due to outer being explicitly unsubscripted
        );

        ys.Subscriptions.AssertEqual(
            Subscribe(100, 250) // ends early, due to outer being explicitly unsubscripted
        );
    }
}