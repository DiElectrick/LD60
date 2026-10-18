using UnityEngine;

/// <summary>
/// Development logging: the editor and development builds keep the play-by-play, a release
/// build contains none of it.
/// <para>
/// The calls are removed by the compiler itself - <see cref="System.Diagnostics.ConditionalAttribute"/>
/// drops the call site (and its arguments, string interpolation included) whenever the symbol is
/// not defined. That is why release logs cost nothing and why a shipped build never spews into
/// the log file. Use plain <c>Debug.LogWarning</c> / <c>Debug.LogError</c> for things that must
/// still be reported in a release build.
/// </para>
/// </summary>
public static class DevLog
{
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Info(object message)
    {
        Debug.Log(message);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Info(object message, Object context)
    {
        Debug.Log(message, context);
    }
}
