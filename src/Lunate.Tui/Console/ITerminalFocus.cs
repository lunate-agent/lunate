namespace Lunate.Tui;

/// <summary>
/// Optional focus reporting on a console. <see cref="IsFocused"/> is null until the terminal sends
/// a focus report, and stays null for a terminal that does not implement the xterm request.
/// Internal so the public <see cref="IConsoleIO"/> contract does not grow a presentation detail.
/// </summary>
internal interface ITerminalFocus
{
    bool? IsFocused { get; }
}
