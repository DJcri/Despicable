using System;
using System.Globalization;
using System.IO;
using System.Xml;
using UnityEngine;
using Verse;

namespace Despicable.AnimModule.AnimGroupStudio.Export;
internal static class AgsExportUtil
{
    public static string MakeVariationDefName(string baseDefName, string code)
    {
        if (baseDefName.NullOrEmpty()) baseDefName = "AGD_Export";
        code = NormalizeTag(code);
        if (code == "") return baseDefName;
        return baseDefName + "_" + code;
    }

    public static string NormalizeTag(string s)
    {
        s ??= "";
        s = s.Trim();
        if (s.Length == 0) return "";
        var chars = s.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
            if (!ok) chars[i] = '_';
        }
        return new string(chars);
    }

    public static string MakeSafeDefName(string s)
    {
        if (s.NullOrEmpty()) s = "AGS_Export";
        s = s.Trim();
        var chars = s.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (char.IsLetterOrDigit(c) || c == '_') continue;
            chars[i] = '_';
        }
        var cleaned = new string(chars);
        if (cleaned.Length == 0) cleaned = "AGS_Export";
        if (char.IsDigit(cleaned[0])) cleaned = "AGS_" + cleaned;
        return cleaned;
    }

    public static bool IsExactDefName(string s)
    {
        if (s.NullOrEmpty()) return false;
        if (s != s.Trim()) return false;
        return MakeSafeDefName(s) == s;
    }

    public static string MakeSafeFileName(string s)
    {
        if (s.NullOrEmpty()) s = "AGS_Export";
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var c in invalid) s = s.Replace(c, '_');
        s = s.Replace(' ', '_');
        if (s.Length == 0) s = "AGS_Export";
        return s;
    }

    public static string MakeExportProjectKey(string baseDefName)
    {
        return MakeSafeFileName(MakeSafeDefName(baseDefName));
    }

    public static string MakeGroupPackageFileName(string projectKey) => $"GroupAnimation_{MakeExportProjectKey(projectKey)}.xml";

    public static string MakeOffsetPackageFileName(string projectKey) => $"OffsetDefs_{MakeExportProjectKey(projectKey)}.xml";

    public static string MakeVariantCode(string variantId)
    {
        if (variantId.NullOrEmpty() || variantId == "Base")
            return "";
        return NormalizeTag(variantId);
    }

    public static string MakeGroupDefName(string projectKey, string variantId)
    {
        string code = MakeVariantCode(variantId);
        if (code == "") return MakeSafeDefName(projectKey);
        return MakeSafeDefName($"{projectKey}_{code}");
    }

    public static string MakeRoleDefName(string projectKey, string variantId, string roleKey)
    {
        string safeProject = MakeSafeDefName(projectKey);
        string safeRole = MakeSafeDefName(roleKey);
        string code = MakeVariantCode(variantId);
        if (code == "")
            return $"Role_{safeProject}_{safeRole}";
        return $"Role_{safeProject}_{code}_{safeRole}";
    }

    public static string MakeOffsetDefName(string projectKey, string roleKey)
    {
        return $"Offset_{MakeSafeDefName(projectKey)}_{MakeSafeDefName(roleKey)}";
    }

    public static string MakeStageToken(int stageIndex, string variantId)
    {
        string stageToken = $"Stage{Math.Max(1, stageIndex + 1)}";
        string code = MakeVariantCode(variantId);
        if (code == "") return stageToken;
        return $"{stageToken}_{code}";
    }

    public static string MakeStageFileName(string projectKey, int stageIndex, string variantId)
    {
        return $"{MakeExportProjectKey(projectKey)}_{MakeStageToken(stageIndex, variantId)}.xml";
    }

    public static string MakeAnimationDefName(string projectKey, string roleKey, int stageIndex, string variantId)
    {
        return MakeSafeDefName($"{projectKey}_{MakeSafeDefName(roleKey)}_{MakeStageToken(stageIndex, variantId)}");
    }

    public static bool IsFinite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    public static bool IsFinite(Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);

    public static bool CanWriteToDirectory(string dir, out string error)
    {
        error = null;
        try
        {
            if (dir.NullOrEmpty()) { error = "dir is empty"; return false; }
            if (!Directory.Exists(dir)) { error = "dir does not exist"; return false; }
            string test = Path.Combine(dir, ".ags_write_test_" + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(test, "test");
            File.Delete(test);
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }

    public static string Vec3ToString(Vector3 v)
    {
        return string.Format(CultureInfo.InvariantCulture, "({0}, {1}, {2})", v.x, v.y, v.z);
    }

    public static void WriteElement(XmlWriter w, string name, string value)
    {
        w.WriteStartElement(name);
        w.WriteString(value ?? "");
        w.WriteEndElement();
    }

    public static void WriteXmlAtomic(string fullPath, Action<XmlWriter> write)
    {
        if (fullPath.NullOrEmpty()) throw new ArgumentNullException(nameof(fullPath));
        if (write == null) throw new ArgumentNullException(nameof(write));

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        string tmp = fullPath + ".tmp";

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = false
        };

        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.Read))
        using (var w = XmlWriter.Create(fs, settings))
        {
            write(w);
        }

        try
        {
            if (File.Exists(fullPath))
                File.Replace(tmp, fullPath, null);
            else
                File.Move(tmp, fullPath);
        }
        catch
        {
            File.Copy(tmp, fullPath, true);
            try { File.Delete(tmp); } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsExportUtil.EmptyCatch:1", "AGS export utility best-effort cleanup failed.", e); }
        }
    }
}
