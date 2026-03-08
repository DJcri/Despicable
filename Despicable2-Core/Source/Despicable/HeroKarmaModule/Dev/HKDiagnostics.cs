using System;
using System.Collections.Generic;
using Verse;

namespace Despicable.HeroKarma;
/// <summary>
/// Lightweight diagnostics sink for DevMode.
/// - Keeps a small ring buffer (always, in DevMode)
/// - Can optionally echo to RimWorld log when enabled
/// Intended to make "silent fails" visible during development without spamming release logs.
/// </summary>
public static class HKDiagnostics
{
    private const int MaxLines = 250;
    private static readonly List<string> recentLines = new(MaxLines);

    // Diagnostics gate.
// In addition to DevMode, allow turning diagnostics on via mod settings so "silent no-ops"
// can be debugged on normal saves without toggling DevMode.
public static bool Enabled
{
    get
    {
        if (Prefs.DevMode) return true;
        try { return HKSettingsUtil.DebugUI || HKSettingsUtil.EchoDiagnosticsToLog; }
        catch { return false; }
    }
}

    private static readonly DiagnosticSessionState SessionState = new();

    // Legacy toggle (Debug tab).
    public static bool EchoToLogEnabled
    {
        get { return SessionState.EchoToLogEnabled; }
        set { SessionState.EchoToLogEnabled = value; }
    }

    public static bool EchoToLog
    {
        get { return EchoToLogEnabled; }
        set { EchoToLogEnabled = value; }
    }

    public static string FirstExceptionContext
    {
        get { return SessionState.FirstExceptionContext; }
    }

    public static string FirstExceptionType
    {
        get { return SessionState.FirstExceptionType; }
    }

    public static IEnumerable<string> Lines
    {
        get { return recentLines; }
    }

    private static bool EchoToLogFromSettings()
    {
        try { return HKSettingsUtil.EchoDiagnosticsToLog; }
        catch { return false; }
    }

    /// <summary>
    /// Add a line to the buffer only (no Log.Message/Warning/Error), DevMode only.
    /// </summary>
    public static void AddOnly(string msg)
    {
        if (!Enabled) return;
        string line = "[HeroKarma] " + msg;
        Add(line);
        if (EchoToLogEnabled || EchoToLogFromSettings()) Log.Message(line);
    }

    public static void Info(string msg)
    {
        if (!Enabled) return;
        string line = "[HeroKarma] " + msg;
        Add(line);
        if (EchoToLogEnabled || EchoToLogFromSettings()) Log.Message(line);
    }

    public static void Warn(string msg)
    {
        if (!Enabled) return;
        string line = "[HeroKarma] " + msg;
        Add(line);
        if (EchoToLogEnabled || EchoToLogFromSettings()) Log.Warning(line);
    }

    public static void Error(string msg)
    {
        if (!Enabled) return;
        string line = "[HeroKarma] " + msg;
        Add(line);
        if (EchoToLogEnabled || EchoToLogFromSettings()) Log.Error(line);
    }

    public static void Catch(Exception ex, string context)
    {
        if (!Enabled) return;

        try
        {
            if (string.IsNullOrEmpty(SessionState.FirstExceptionType))
            {
                SessionState.FirstExceptionContext = context;
                SessionState.FirstExceptionType = ex != null ? ex.GetType().Name : "Exception";
            }
        }
        catch
        {
            // ignore
        }

        Warn($"{context} threw {(ex != null ? ex.GetType().Name : "Exception")}");
    }

    public static void ClearFirstException()
    {
        SessionState.FirstExceptionContext = null;
        SessionState.FirstExceptionType = null;
        AddOnly("---- first exception cleared ----");
    }

    private static void Add(string line)
    {
        if (recentLines.Count >= MaxLines) recentLines.RemoveAt(0);
        recentLines.Add(line);
    }

    public static void ResetRuntimeState()
    {
        recentLines.Clear();
        SessionState.FirstExceptionContext = null;
        SessionState.FirstExceptionType = null;
    }

    public static void ResetSessionState()
    {
        ResetRuntimeState();
    }

    private sealed class DiagnosticSessionState
    {
        public bool EchoToLogEnabled;
        public string FirstExceptionContext;
        public string FirstExceptionType;
    }
}
