namespace SnapZones.Core.Geometry;

public sealed record ZoneValidationError(string Code, Guid? ZoneId, string Message);

public sealed record ZoneValidationResult(IReadOnlyList<ZoneValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
