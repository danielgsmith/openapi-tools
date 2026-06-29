using OpenApiTools.Models;

namespace OpenApiTools.Services;

public interface IOpenApiConflictDetector
{
    MergeComponentConflict Detect<T>(
        string componentLabel,
        string name,
        string incomingOwner,
        IDictionary<string, T> existingComponents,
        IDictionary<string, string> existingOwners,
        Func<T, T, bool> areEquivalent,
        T incoming)
        where T : class;
}

public sealed class OpenApiConflictDetector : IOpenApiConflictDetector
{
    public MergeComponentConflict Detect<T>(
        string componentLabel,
        string name,
        string incomingOwner,
        IDictionary<string, T> existingComponents,
        IDictionary<string, string> existingOwners,
        Func<T, T, bool> areEquivalent,
        T incoming)
        where T : class
    {
        if (!existingComponents.TryGetValue(name, out var existing))
            return new MergeComponentConflict(MergeConflictKind.Unique, componentLabel, name, null, incomingOwner);

        var existingOwner = existingOwners.TryGetValue(name, out var owner) ? owner : null;
        var kind = areEquivalent(existing, incoming)
            ? MergeConflictKind.IdenticalDuplicate
            : MergeConflictKind.ConflictingDuplicate;

        return new MergeComponentConflict(kind, componentLabel, name, existingOwner, incomingOwner);
    }
}
