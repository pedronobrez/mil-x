namespace MilX.Pipeline.Internal;

/// <summary>Synchronous <see cref="IProgress{T}"/> (unlike <see cref="Progress{T}"/> it does not post to a SynchronizationContext).</summary>
internal sealed class DelegateProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;
    public DelegateProgress(Action<T> handler) => _handler = handler;
    public void Report(T value) => _handler(value);
}
