namespace Memora.Import.Prerequisites;

public sealed record RuntimePrerequisiteDiagnostic(string Code, string Message, string Tool);

public sealed class RuntimePrerequisiteResult
{
    public RuntimePrerequisiteResult(IReadOnlyList<RuntimePrerequisiteDiagnostic> diagnostics)
    {
        Diagnostics = diagnostics?.ToArray() ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public bool IsReady => Diagnostics.Count == 0;

    public IReadOnlyList<RuntimePrerequisiteDiagnostic> Diagnostics { get; }
}
