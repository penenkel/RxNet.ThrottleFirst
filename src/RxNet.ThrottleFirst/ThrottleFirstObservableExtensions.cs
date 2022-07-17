using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace RxNet.ThrottleFirst;

public static class ThrottleFirstObservableExtensions
{
    ///<inheritdoc cref="ThrottleFirst{T}(IObservable{T}, TimeSpan, IScheduler)"/>
    public static IObservable<T> ThrottleFirst<T>(this IObservable<T> source, TimeSpan suppressionPeriod)
        => ThrottleFirst(source, suppressionPeriod, Scheduler.Default);

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
        if (suppressionPeriod < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(suppressionPeriod), "Cannot be negative.");
        if (scheduler is null)
            throw new ArgumentNullException(nameof(scheduler));
        return ThrottleFirst(source, item => Observable.Timer(suppressionPeriod, scheduler));
    }

    /// <summary>
    /// Emits the <i>first</i> item from the <paramref name="source"/> and then skips subsequent
    /// items until the Observable provided by <paramref name="suppressionPeriodSelector"/> emits.
    /// The next source item after that is emitted again and the suppression period starts anew by
    /// selection the next suppression period Observable.
    /// </summary>
    /// <typeparam name="T">Type of the items in <paramref name="source"/></typeparam>
    /// <typeparam name="TThrottle">
    /// Type of the items in the observable produced by <paramref
    /// name="suppressionPeriodSelector"/>. The exact type is irrelevant for the purpose of the operator.
    /// </typeparam>
    /// <param name="source">The source Observable</param>
    /// <param name="suppressionPeriodSelector">
    /// A function that accepts the source item as input and returns an Observable. The first time
    /// that Observable emits, signals the end of the suppression period.
    /// </param>
    /// <returns>The throttled source</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static IObservable<T> ThrottleFirst<T, TThrottle>(this IObservable<T> source, Func<T, IObservable<TThrottle>> suppressionPeriodSelector)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));
        if (suppressionPeriodSelector is null)
            throw new ArgumentNullException(nameof(suppressionPeriodSelector));
        return Observable.Create<T>(observer =>
        {
            T currentValue;
            IDisposable? throttling = null;

            void Send(T value)
            {
                currentValue = value;
                observer.OnNext(currentValue);
                StartThrottling(value);
            }

            void StartThrottling(T value)
            {
                throttling = suppressionPeriodSelector(value).Subscribe(
                    _ => EndThrottling(),
                    observer.OnError,
                    EndThrottling);
            }

            void EndThrottling()
            {
                throttling?.Dispose();
                throttling = null;
            };

            var subscription = source.Subscribe(
                value =>
                {
                    if (throttling is null)
                        Send(value);
                },
                observer.OnError,
                observer.OnCompleted
            );

            // NOTE: If the outer observable completes or ends with an error, the correct disposal
            // of the inner observable depends on the fact that the outer subscription is
            // automatically unsubscripted. Within RxNet this should be the case for all well formed observables.

            return Disposable.Create(() =>
            {
                subscription.Dispose();
                throttling?.Dispose();
            });
        });
    }
}