using System.Collections.Generic;
using RuriLib.Models.Bots;

namespace RuriLib.Models.Runner;

public class RunnerResult
{
    public required BotData BotData { get; init; }
    public required List<Capture> Captures { get; init; }
}
