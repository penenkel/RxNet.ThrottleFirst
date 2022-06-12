using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace RxNet.ThrottleFirst;

public static class ThrottleFirstObservableExtensions
{
    ///<inheritdoc cref="ThrottleFirst{T}(IObservable{T}, TimeSpan, IScheduler)"/>
    public static IObservable<T> ThrottleFirst<T>(this IObservable<T> source, TimeSpan suppressionPeriod)
        => source.ThrottleFirst(suppressionPeriod, Scheduler.Default);

    /// <summary>
    /// Emits the <i>first</i> item from the <paramref name="source"/> and then skips subsequent
    /// items until the given <paramref name="suppressionPeriod"/> has elapsed. The next source item
    /// after that is emitted again and the suppression period starts anew.
    /// </summary>
    /// <typeparam name="T">Type of the items in <paramref name="source"/></typeparam>
    /// <param name="source">The source Observable</param>
    /// <param name="suppressionPeriod">The duration for which subsequent items are skipped</param>
    /// <param name="scheduler"></param>
    /// <returns>The throttled source</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static IObservable<T> ThrottleFirst<T>(this IObservable<T> source, TimeSpan suppressionPeriod, IScheduler scheduler)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (suppressionPeriod < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(suppressionPeriod), "must not be negative");
        if (scheduler is null)
            throw new ArgumentNullException(nameof(scheduler));
        return source.ThrottleFirst(item => Observable.Timer(suppressionPeriod, scheduler));
    }

    /// <summary>
    /// Emits the <i>first</i> item from the <paramref name="source"/> and then skips subsequent
    /// items until the Observable provided by <paramref name="supressionPeriodSelector"/> emits.
    /// The next source item after that is emitted again and the suppression period starts anew by
    /// selection the next suppression period Observable.
    /// </summary>
    /// <typeparam name="T">Type of the items in <paramref name="source"/></typeparam>
    /// <typeparam name="TThrottle">
    /// Type of the items in the observable produced by <paramref name="supressionPeriodSelector"/>.
    /// The exact type is irrelevant for the purpose of the operator.
    /// </typeparam>
    /// <param name="source">The source Observable</param>
    /// <param name="supressionPeriodSelector">
    /// A function that accepts the source item as input and returns an Observable. The first time
    /// that Observable emits, signals the end of the suppression period.
    /// </param>
    /// <returns>The throttled source</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static IObservable<T> ThrottleFirst<T, TThrottle>(this IObservable<T> source, Func<T, IObservable<TThrottle>> supressionPeriodSelector)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (supressionPeriodSelector is null)
            throw new ArgumentNullException(nameof(supressionPeriodSelector));
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
                throttled = supressionPeriodSelector(value).Subscribe(EndThrottling, observer.OnError, CleanupThrottling);
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