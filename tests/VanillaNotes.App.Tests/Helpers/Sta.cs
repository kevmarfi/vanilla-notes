using System.Runtime.ExceptionServices;

namespace VanillaNotes.App.Tests.Helpers;

/// <summary>
/// Runs the body of a test on a thread of its own in the single-threaded apartment, the only one
/// the documents and controls of WPF can be made on. A failure inside is thrown again as it was.
/// </summary>
internal static class Sta
{
    /// <summary>Runs an action on a thread of the single-threaded apartment, and waits for it.</summary>
    public static void Run(Action body)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }
}
