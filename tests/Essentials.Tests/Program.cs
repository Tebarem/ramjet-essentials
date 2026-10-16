namespace Mc.Scripting.Essentials.Tests;

/// <summary>Console test runner: each test throws when it fails; the exit code is non-zero when any test failed.</summary>
internal static class Program
{
    private static readonly List<(string Name, Action Test)> Tests = new();

    public static int Main()
    {
        void Add(string name, Action test) => Tests.Add((name, test));
        CoreTests.Register(Add);
        TeleportTests.Register(Add);
        EconomyTests.Register(Add);
        SettingsTests.Register(Add);

        int failed = 0;
        foreach (var (name, test) in Tests)
        {
            try
            {
                test();
                Console.WriteLine($"ok    {name}");
            }
            catch (Exception e)
            {
                failed++;
                Console.WriteLine($"FAIL  {name}: {e.Message}");
            }
        }
        Console.WriteLine($"{Tests.Count - failed}/{Tests.Count} passed");
        return failed == 0 ? 0 : 1;
    }
}

/// <summary>Assertions that throw with a message.</summary>
internal static class Check
{
    public static void Equal<T>(T expected, T actual, string what)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{what}: expected {expected}, got {actual}");
        }
    }

    public static void True(bool condition, string what)
    {
        if (!condition) throw new InvalidOperationException(what);
    }
}
