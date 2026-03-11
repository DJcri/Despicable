using System;
using System.Globalization;
using System.IO;
using System.Xml;
using UnityEngine;
using Verse;

namespace Despicable.AnimModule.AnimGroupStudio.Export;
internal static class AgsExportUtil
{
    public static string MakeVariationDefName(string baseDefName, string label)
    {
        string safeBase = MakeSafeDefName(baseDefName);
        string code = MakeVariationCode(label);
        if (code == "") return safeBase;
        return safeBase + "_" + code;
    }

    public static string MakeVariationCode(string label)
    {
        return NormalizeTag(label);
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

    public static string MakeGroupPackageFileName(string familyKey) => $"GroupAnimation_{MakeExportProjectKey(familyKey)}.xml";

    public static string MakeOffsetPackageFileName(string familyKey) => $"OffsetDefs_{MakeExportProjectKey(familyKey)}.xml";

    public static string MakeGroupDefName(string baseDefName, string label)
    {
        return MakeVariationDefName(baseDefName, label);
    }

    public static string MakeRoleDefName(string baseDefName, string label, string roleKey)
    {
        string safeVariation = MakeVariationDefName(baseDefName, label);
        string safeRole = MakeSafeDefName(roleKey);
        return $"Role_{safeVariation}_{safeRole}";
    }

    public static string MakeOffsetDefName(string baseDefName, string label, string roleKey)
    {
        string safeVariation = MakeVariationDefName(baseDefName, label);
        string safeRole = MakeSafeDefName(roleKey);
        return $"Offset_{safeVariation}_{safeRole}";
    }

    public static string MakeStageToken(int stageIndex, string label)
    {
        string stageToken = $"Stage{Math.Max(1, stageIndex + 1)}";
        string code = MakeVariationCode(label);
        if (code == "") return stageToken;
        return $"{stageToken}_{code}";
    }

    public static string MakeStageFileName(string baseDefName, int stageIndex, string label)
    {
        return $"{MakeExportProjectKey(baseDefName)}_{MakeStageToken(stageIndex, label)}.xml";
    }

    public static bool IsStageFileForVariation(string fileName, string baseDefName, string label)
    {
        if (fileName.NullOrEmpty())
            return false;

        string stem = Path.GetFileNameWithoutExtension(fileName);
        string prefix = MakeExportProjectKey(baseDefName) + "_";
        if (!stem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        string tail = stem.Substring(prefix.Length);
        if (!tail.StartsWith("Stage", StringComparison.OrdinalIgnoreCase))
            return false;

        string code = MakeVariationCode(label);
        int us = tail.IndexOf('_');
        if (code == "")
            return us < 0;
        if (us < 0 || us >= tail.Length - 1)
            return false;
        return string.Equals(tail.Substring(us + 1), code, StringComparison.Ordinal);
    }

    public static string MakeAnimationDefName(string baseDefName, string label, string roleKey, int stageIndex)
    {
        return MakeSafeDefName($"{MakeVariationDefName(baseDefName, label)}_{MakeSafeDefName(roleKey)}_{MakeStageToken(stageIndex, label)}");
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
