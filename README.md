# Purpose

Provides the `ThrottleFirst` filtering operator for Rx.Net.

![marble-throttlefirst](https://raw.githubusercontent.com/penenkel/RxNet.ThrottleFirst/master/assets/throttleFirst.png)

Given a source Observable and a suppression period TimeSpan: Emit the _first_ item from the source. Then skip subsequent items until the given period has elapsed. Then emit the next source item and start the suppression period anew.
```
IObservable<T> ThrottleFirst<T>(this IObservable<T> source, TimeSpan suppressionPeriod)
```

There is also a version where the suppression period is provided by a selector function. That function accepts the emitted source item as input and returns an Observable. Which in turn signals the end of the suppression period when it emits.
```
IObservable<T> ThrottleFirst<T, TThrottle>(this IObservable<T> source, Func<T, IObservable<TThrottle>> suppressionPeriodSelector)
```

# Motivation

I recently needed an operator implementing the behavior as described in the previous section. But while there is an operator named `Throttle` in Rx.Net, what it does would be called `debounce` in other languages. See [Background](#Background) for a more thorough explanation.

There is an issue asking for such an operator [Implement a real throttle (not debounce, like the existing throttle)](https://github.com/dotnet/reactive/issues/395), but it has been closed citing that a) Rx.Net was there first and as such their naming shouldn't have to conform to others and b) the maintenance cost of yet an other operator.

And while several "true" throttle variants where sketched in that thread, none included the selector variant and of cause there where no tests. Thus, the decision to create my own implementation including a though test suite.

# Implementation

The implementation of the actual operator is heavily inspired by the rxjs variant. Particularly because I still haven't quite understood how the "native" Rx.Net operator implementations work.
The unit-tests however are kept in style with Rx.Net and utilize the `TestScheduler`.

# Background

Unfortunately there is significant disparity between the various reactive implementations and what they call their operators. If we just consider the filtering operators, that is,  operators that reduce the number of items emitted involving some time period, they are usually named some variation of `sample`, `throttle` and `debounce`.

reactivex.io only lists and defines two of those operators directly: `sample` and `debounce`.  

> `sample` emits the most recent item emitted by an Observable within periodic time intervals. This operator is sometimes also called `throttleLast`.
![marble-sample](https://raw.githubusercontent.com/penenkel/RxNet.ThrottleFirst/master/assets/sample.png)

> `debounce` only emits an item from an Observable if a particular timespan has passed without it emitting another item. This operator is sometimes also called `throttleWithTimeout`.
![marble-debounce](https://raw.githubusercontent.com/penenkel/RxNet.ThrottleFirst/master/assets/debounce.png)


But in addition to that, one the page for `sample`, they also define an operator named `throttleFirst`:

> `throttleFirst` emits the first item and then skips items until the period elapses.
![marble-throttlefirst](https://raw.githubusercontent.com/penenkel/RxNet.ThrottleFirst/master/assets/throttleFirst.png)




In Rx.Net however, the behavior of the `Throttle` operator matches the definition of `debounce` from above, which makes the confusion complete.

---

As a side note: In my opinion, the newer, interactive marble diagrams on reactivex.io do not help the matter, due to the fact that they cannot express time-spans like the older, static once do. But at least those are still available, even if they are hidden in the implementation specific expanders.





