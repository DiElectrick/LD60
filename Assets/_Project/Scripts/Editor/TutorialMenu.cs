using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor helpers for the first-run tutorial: forget the "already shown" flag so the next
/// play starts with the hand again. Handy while the tutorial is being tuned.
/// </summary>
public static class TutorialMenu
{
    const string ResetPath = "Tools/LD60/Reset tutorial (show it on the next run)";

    [MenuItem(ResetPath)]
    public static void ResetTutorial()
    {
        PlayerPrefs.DeleteKey(TutorialController.ShownKey);
        PlayerPrefs.Save();

        DevLog.Info($"[Tutorial] Progress reset ('{TutorialController.ShownKey}') - " +
                  "the tutorial shows up on the next run");
    }

    [MenuItem(ResetPath, true)]
    static bool ResetTutorialValidate()
    {
        // Clickable only outside play mode: the running game would just set the flag again.
        return !EditorApplication.isPlaying;
    }
}
