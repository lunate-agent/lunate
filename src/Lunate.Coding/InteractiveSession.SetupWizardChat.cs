using Lunate.Ai;

namespace Lunate.Coding;

// The chat usage's provider/model route (spec: Fast and decision usages from any provider):
// "change the chat profile" moves the current profile to a model another configured provider
// serves and binds the usage, without touching any other profile.

internal sealed partial class InteractiveSession
{
    /// <summary>
    /// Moves the chat profile to the picked model's provider and model, keeps its name, asks the
    /// thinking level the new model declares, and binds the usage. A refusal (a decision-only
    /// family) is the shared chat-binding refusal and leaves the step open.
    /// </summary>
    private async Task<WizardMove> MoveChatProfileAsync(
        SetupWizardModel wizard,
        WizardModelRef model,
        CancellationToken ct
    )
    {
        ModelProfile? current = CurrentChatProfile();
        string name;
        if (current is { } profile)
        {
            name = profile.Name;
        }
        else
        {
            string? typed = await PromptAsync("Profile name (no spaces or '/')", ct)
                .ConfigureAwait(false);
            if (typed is null)
            {
                return WizardMove.Abort;
            }

            name = typed.Trim();
            if (name.Length == 0)
            {
                wizard.Skip(SetupStep.ChatProfile);
                return WizardMove.Advance;
            }

            if (name.Any(char.IsWhiteSpace) || name.Contains('/'))
            {
                Notify(
                    "a profile name must have no whitespace or '/'; the step asks again",
                    NoticeLevel.Warning
                );
                return WizardMove.Stay;
            }
        }

        ModelInfo info = WizardModelCheck.ModelFor(model, _catalog);
        string? declared = model.Model.Reasoning ?? info.Reasoning;
        string? level = null;
        if (declared is "effort" or "budget")
        {
            string? fallback = current?.ReasoningLevel is "low" or "medium" or "high"
                ? current.ReasoningLevel
                : null;
            string? selected = await PickAsync(
                    $"Thinking level for {model.Model.ModelId}",
                    ["low", "medium", "high"],
                    ["low", "medium", "high"],
                    PickerKind.WizardChoice,
                    ct,
                    fallback
                )
                .ConfigureAwait(false);
            if (selected is null)
            {
                return WizardMove.Abort;
            }

            level = selected;
        }

        try
        {
            UsageProfileValidation.RequireChatFamily(
                name,
                model.Model.ModelId,
                model.Model.ApiFamily,
                SetupState.ChatUsage
            );
        }
        catch (InvalidDataException exception) when (UsageProfileValidation.IsRefusal(exception))
        {
            Notify(exception.Message, NoticeLevel.Warning);
            return WizardMove.Stay;
        }

        wizard.RecordChatProfile(
            new WizardProfileChoice(
                name,
                model.Model.ModelId,
                level,
                model.Provider.Id,
                model.Model.ApiFamily
            )
        );
        return WizardMove.Advance;
    }
}
