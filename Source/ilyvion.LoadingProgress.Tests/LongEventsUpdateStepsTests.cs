using DevTools.Testing;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class LongEventsUpdateStepsTests
{
    // Clear of the keys the patches use, so the test's error is never one already logged once.
    private const int TestErrorKey = 0x4C50_7F00;

    [Test]
    [ErrorsAllowed("A long event update step failed")]
    public static void AStepThatThrowsLeavesTheStepsAfterItToRun()
    {
        var ran = new List<int>();

        LongEventHandler_LongEventsUpdate_Patches.RunEach(
            [
                () => ran.Add(0),
                () => throw new InvalidOperationException("A step that fails, for the test."),
                () => ran.Add(2),
            ],
            TestErrorKey
        );

        Expect.AreEqual(ran.Count, 2);
        Expect.IsTrue(ran.Contains(0));
        Expect.IsTrue(ran.Contains(2));
    }
}
