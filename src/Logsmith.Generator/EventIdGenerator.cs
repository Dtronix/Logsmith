namespace Logsmith.Generator;

internal static class EventIdGenerator
{
    /// <summary>
    /// Returns stable FNV-1a hash of "ClassName.MethodName", or the user-specified
    /// eventId if nonzero.
    /// </summary>
    internal static int Generate(string className, string methodName, int userSpecified)
    {
        if (userSpecified != 0)
            return userSpecified;

        return Fnv1aHash(className + "." + methodName);
    }

    private static int Fnv1aHash(string input)
    {
        unchecked
        {
            const uint offsetBasis = 2166136261u;
            const uint prime = 16777619u;

            uint hash = offsetBasis;
            for (int i = 0; i < input.Length; i++)
            {
                hash ^= input[i];
                hash *= prime;
            }
            return (int)hash;
        }
    }
}
