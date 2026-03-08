using System.Collections.Generic;
using UnityEngine;

namespace Despicable;
/// <summary>
/// Prevents the same UI action from opening duplicate dialogs within a single rendered frame.
/// </summary>
public static class SingleFrameDialogGate
{
    private static readonly Dictionary<string, int> LastFrameByKey = new();

    public static bool TryEnter(string gateKey)
    {
        if (string.IsNullOrEmpty(gateKey))
            return true;

        int now = Time.frameCount;
        if (LastFrameByKey.TryGetValue(gateKey, out int lastFrame) && lastFrame == now)
            return false;

        LastFrameByKey[gateKey] = now;
        return true;
    }

    public static void ResetRuntimeState()
    {
        LastFrameByKey.Clear();
    }
}
