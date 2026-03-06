namespace Phantasma.Tomb.Validation;

/// <summary>
/// TOMB-specific method naming rules that are not present in the Phoenix SDK.
/// We keep this in TOMB validation namespace as a direct compiler rule set.
/// </summary>
public static class MethodNameValidation
{
    /// <summary>
    /// Enforces TOMB naming conventions used by compiler checks:
    /// - isXxx => bool
    /// - onXxx => none
    /// - getXxx => non-none
    /// </summary>
    public static bool IsValidMethod(string methodName, VMType returnType)
    {
        if (string.IsNullOrEmpty(methodName) || methodName.Length < 3)
        {
            return false;
        }

        if (methodName.StartsWith("is") && char.IsUpper(methodName[2]))
        {
            return returnType == VMType.Bool;
        }

        if (methodName.StartsWith("on") && char.IsUpper(methodName[2]))
        {
            return returnType == VMType.None;
        }

        if (methodName.StartsWith("get") && methodName.Length >= 4 && char.IsUpper(methodName[3]))
        {
            return returnType != VMType.None;
        }

        return true;
    }
}
