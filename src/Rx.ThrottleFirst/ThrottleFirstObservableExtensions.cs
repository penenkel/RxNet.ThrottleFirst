using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Rx.ThrottleFirst;

public static class ThrottleFirstObservableExtensions
{
    ///<inheritdoc cref="ThrottleFirst{T}(IObservable{T}, TimeSpan, IScheduler)"/>
    public static IObservable<T> ThrottleFirst<T>(this IObservable<T> source, TimeSpan duration)
        => source.ThrottleFirst(duration, Scheduler.Default);

    /// <summary>
    /// Emits the <i>first</i> item from the <paramref name="source"/> and then skips subsequent
    /// items until the given <paramref name="duration"/> has elapsed. After that, the next source
    /// item is emitted again and the suppression period starts anew.
    /// </summary>
    /// <typeparam name="T">Type of the items in <paramref name="source"/></typeparam>
    /// <param name="source">The source Observable</param>
    /// <param name="duration">The duration for which subsequent items are skipped</param>
    /// <param name="scheduler"></param>
    /// <returns>The throttled source</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static IObservable<T> ThrottleFirst<T>(this IObservable<T> source, TimeSpan duration, IScheduler scheduler)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (duration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duration), "must not be negative");
        if (scheduler is null)
            throw new ArgumentNullException(nameof(scheduler));
        return source.ThrottleFirst(item => Observable.Timer(duration, scheduler));
    }

    /// <summary>
    /// Emits the <i>first</i> item from the <paramref name="source"/> and then skips subsequent
    /// items until the Observable provided by <paramref name="durationSelector"/> emits. After
    /// that, the next source item is emitted again and the suppression period starts anew by
    /// selection the next duration observable.
    /// </summary>
    /// <typeparam name="T">Type of the items in <paramref name="source"/></typeparam>
    /// <typeparam name="TThrottle">
    /// Type of the items in the observable produced by <paramref name="durationSelector"/>. The
    /// exact type is irrelevant for the purpose of the operator.
    /// </typeparam>
    /// <param name="source">The source Observable</param>
    /// <param name="durationSelector">
    /// A function that accepts the source item as input and returns an Observable. The first time
    /// that Observable emits, signals the end of the suppression period.
    /// </param>
    /// <returns>The throttled source</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static IObservable<T> ThrottleFirst<T, TThrottle>(this IObservable<T> source, Func<T, IObservable<TThrottle>> durationSelector)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (durationSelector is null)
            throw new ArgumentNullException(nameof(durationSelector));
        return Observable.Create<T>(observer =>
        {
            T currentValue;
            bool isComplete = false;
            IDisposable? throttled = null;

            void Send(T value)
            {
                currentValue = value;
                observer.OnNext(currentValue);
                if (!isComplete)
                    StartThrottle(value);
            }

            void StartThrottle(T value)
            {
                throttled = durationSelector(value).Subscribe(EndThrottling, observer.OnError, CleanupThrottling);
            }

            void EndThrottling(TThrottle throttle)
            {
                throttled?.Dispose();
                throttled = null;
            };

            void CleanupThrottling()
            {
                throttled = null;
                if (isComplete)
                    observer.OnCompleted();
            };

            var subscription = source.Subscribe(
                value =>
                {
                    if (throttled is null)
                        Send(value);
                },
                observer.OnError,
                () =>
                {
                    isComplete = true;
                    observer.OnCompleted();
                }
            );

            return Disposable.Create(() =>
            {
                subscription.Dispose();
                throttled?.Dispose();
            });
        });
    }
}