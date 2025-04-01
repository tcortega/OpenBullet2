using System;
using System.Collections.Generic;

namespace RuriLib.Logging;

public class NoOpLogger : IBotLogger
{
    public bool Enabled { get; set; }
    public IEnumerable<BotLoggerEntry> Entries { get; }
    public string ExecutingBlock { get; set; }
    public event EventHandler<BotLoggerEntry>? NewEntry;

    public void LogHeader(string caller = null)
    {
        // skip
    }

    public void Log(string message, string color = "#fff", bool canViewAsHtml = false)
    {
        // skip
    }

    public void Log(IEnumerable<string> enumerable, string color = "#fff", bool canViewAsHtml = false)
    {
        // skip
    }

    public void Clear()
    {
        // skip
    }

    public void LogObject(object obj, string color = "#fff", bool canViewAsHtml = false)
    {
        // skip
    }
}
