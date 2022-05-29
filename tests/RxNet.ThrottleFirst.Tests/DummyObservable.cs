namespace RxNet.ThrottleFirst.Tests;

internal class DummyObservable<T> : IObservable<T>
{
    public static readonly DummyObservable<T> Instance = new();

    private DummyObservable()
    {
    }

    public IDisposable Subscribe(IObserver<T> observer)
    {
        throw new NotImplementedException();
    }
}