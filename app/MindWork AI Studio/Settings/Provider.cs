using AIStudio.Provider;

namespace AIStudio.Settings;

/// <summary>
/// Data model for configured providers.
/// </summary>
/// <param name="Num">The provider's number.</param>
/// <param name="Id">The provider's ID.</param>
/// <param name="InstanceName">The provider's instance name. Useful for multiple instances of the same provider, e.g., to distinguish between different OpenAI API keys.</param>
/// <param name="UsedProvider">The provider used.</param>
/// <param name="Model">The LLM model to use for chat.</param>
public readonly record struct Provider(uint Num, string Id, string InstanceName, Providers UsedProvider, Model Model)
{
    #region Overrides of ValueType

    /// <summary>
    /// Returns a string that represents the current provider in a human-readable format.
    /// We use this to display the provider in the chat UI.
    /// </summary>
    /// <returns>A string that represents the current provider in a human-readable format.</returns>
    public override string ToString()
    {
        return $"{this.InstanceName} ({this.UsedProvider.ToName()}, {this.Model})";
    }

    #endregion
}