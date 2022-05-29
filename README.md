# Purpose

Provides the `ThrottleFirst` filtering operator for RxNET.

![](https://reactivex.io/documentation/operators/images/throttleFirst.png){height=250}

Given a source Observable and a duration, it emits the _first_ item from the source and then skips subsequent items until the given duration has elapsed. The next source item after that is emitted again and the duration starts anew.
```
IObservable<T> ThrottleFirst<T>(this IObservable<T> source, TimeSpan duration)
```

Ther is also a version where the duration is provided by a selector function. That function accepts the source item as input and returns an Observable, which in turn signals the end of the duration when it emits.
```
IObservable<T> ThrottleFirst<T, TThrottle>(this IObservable<T> source, Func<T, IObservable<TThrottle>> durationSelector)
```

# Motivation

There is significant disparity over the various reactive implementations what the various filtering operators are called and what they do.


 `sample`, `debounce`, `throttle`, `throttleFirst`, `throttleLast` all have incommon that they reduce the number of items emitted by involing some kind of time period.

Filter operators

absolute timing
relative timing
emitt first in intervall
emitt last in intervall

![](https://reactivex.io/documentation/operators/images/debounce.png){height=250}

