using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Despicable;

public static class PatchMethodDiscoveryUtil
{
    public static IEnumerable<MethodBase> ExistingMethods(string typeName, params string[] methodNames)
    {
        Type type = AccessTools.TypeByName(typeName);
        if (type == null || methodNames == null || methodNames.Length == 0)
            yield break;

        List<MethodInfo> declaredMethods = AccessTools.GetDeclaredMethods(type);
        if (declaredMethods == null || declaredMethods.Count == 0)
            yield break;

        HashSet<MethodBase> yielded = new();
        for (int i = 0; i < methodNames.Length; i++)
        {
            string methodName = methodNames[i];
            if (string.IsNullOrEmpty(methodName))
                continue;

            for (int j = 0; j < declaredMethods.Count; j++)
            {
                MethodInfo method = declaredMethods[j];
                if (method == null || method.Name != methodName)
                    continue;

                if (yielded.Add(method))
                    yield return method;
            }
        }
    }
}
