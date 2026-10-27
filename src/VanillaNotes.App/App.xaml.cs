using System.Windows;
using System.Windows.Threading;
using VanillaNotes.App.ViewModels;
using VanillaNotes.Core.Errors;
using VanillaNotes.Core.Storage;

namespace VanillaNotes.App;

/// <summary>
/// The start of the application: one running copy, the data folder through the wiring, the
/// texts every view reads, the first page of the window, which is the workspace of the public profile, or
/// the window of profiles when that one cannot be opened, and the backup of the day. A failure nothing else caught ends here:
/// it is written to the record of failures, and the person is told only that something went wrong.
/// </summary>
public partial class App
{
    /// <summary>The name of the lock that tells a second copy of the application that one is already running.</summary>
    private const string _singleCopyName = "VanillaNotes.Desktop.SingleCopy";

    private Mutex? _singleCopy;

    private DiagnosticLog? _diagnostics;

    /// <summary>Whether the person is being told that something went wrong, so that a failure while the notice is shown is only recorded, and does not open another notice over it.</summary>
    private bool _isTellingUnexpected;

    private AppServices? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleCopy = new Mutex(initiallyOwned: true, _singleCopyName, out bool isFirst);
        if (!isFirst)
        {
            // Two copies would write the same data at once: the second one leaves.
            Shutdown();
            return;
        }

        _diagnostics = AppServices.CreateDiagnosticLog();
        DispatcherUnhandledException += OnUnexpected;
        AppDomain.CurrentDomain.UnhandledException += OnUnexpectedElsewhere;
        TaskScheduler.UnobservedTaskException += OnUnobserved;
        _services = new AppServices(_diagnostics);
        Resources["Texts"] = _services.Texts;
        try
        {
            MainViewModel workspace = _services.OpenPublicWorkspace();
            _services.Dialogs.Show(workspace, workspace.Leave);
        }
        catch (AppException failure)
        {
            _services.Dialogs.Show(_services.CreateProfileChooser());
            _services.Dialogs.Notify(_services.Failures.Text(failure));
        }

        _ = BackUpIfDueAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        _singleCopy?.Dispose();
        base.OnExit(e);
    }

    private void OnUnexpected(object sender, DispatcherUnhandledExceptionEventArgs failure)
    {
        failure.Handled = true;
        _diagnostics?.Record(failure.Exception);
        if (_services is null || _isTellingUnexpected)
        {
            return;
        }

        _isTellingUnexpected = true;
        try
        {
            _services.Dialogs.Notify(_services.Failures.Unexpected);
        }
        finally
        {
            _isTellingUnexpected = false;
        }
    }

    /// <summary>
    /// The backup of the day, while the person writes. One that fails was deleted by the service; it
    /// is recorded, and nobody is interrupted for it.
    /// </summary>
    private async Task BackUpIfDueAsync()
    {
        try
        {
            await _services!.BackUpIfDueAsync();
        }
        catch (Exception failure)
        {
            _diagnostics?.Record(failure);
        }
    }

    /// <summary>
    /// A failure on a thread other than the one of the window. Nothing can be shown and the application
    /// stops after it; it is only recorded.
    /// </summary>
    private void OnUnexpectedElsewhere(object sender, UnhandledExceptionEventArgs failure)
    {
        if (failure.ExceptionObject is Exception exception)
        {
            _diagnostics?.Record(exception);
        }
    }

    /// <summary>
    /// A failure of slow work that nothing waited for. It does not stop the application, and nobody is
    /// there to be told; it is only recorded.
    /// </summary>
    private void OnUnobserved(object? sender, UnobservedTaskExceptionEventArgs failure) => _diagnostics?.Record(failure.Exception);
}
