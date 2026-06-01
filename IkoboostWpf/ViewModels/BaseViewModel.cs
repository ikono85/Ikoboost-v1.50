using CommunityToolkit.Mvvm.ComponentModel;

namespace IkoboostWpf.ViewModels;

public abstract class BaseViewModel : ObservableObject, IDisposable
{
    private bool _disposed;

    protected bool IsDisposed => _disposed;

    public virtual void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
