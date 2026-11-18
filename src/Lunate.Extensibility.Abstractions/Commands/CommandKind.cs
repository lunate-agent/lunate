namespace Lunate.Extensibility.Abstractions;

/// <summary>What kind of source contributed a command; the registry orders by kind first.</summary>
public enum CommandKind
{
    /// <summary>A command the CLI ships (the first source, shown by <c>/help</c>).</summary>
    Builtin,

    /// <summary>A discovered skill, invoked as <c>skill:&lt;name&gt;</c>.</summary>
    Skill,

    /// <summary>A prompt-template command; the slot is reserved, no template source ships yet.</summary>
    Template,

    /// <summary>A command an extension provides.</summary>
    Extension,
}
