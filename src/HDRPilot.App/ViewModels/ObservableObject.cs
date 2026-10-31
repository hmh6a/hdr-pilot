using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace HDRPilot.App.ViewModels;
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; Raise(name); return true; }
}
public sealed class RelayCommand(Action<object?> action) : ICommand
{
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => action(parameter);
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}
public sealed class AsyncCommand(Func<object?, Task> action, Action<Exception> onError) : ICommand
{
    private bool running;
    public bool CanExecute(object? parameter) => !running;
    public async void Execute(object? parameter)
    {
        if (running) return; running = true; CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await action(parameter); } catch (Exception ex) { onError(ex); }
        finally { running = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
    }
    public event EventHandler? CanExecuteChanged;
}
