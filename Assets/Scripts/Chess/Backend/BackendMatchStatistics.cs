using System;
using System.Collections.Generic;

[Serializable]
public sealed class BackendMatchStatistics
{
    public int version;
    public int moveCount;
    public long elapsedSeconds;
    public bool historyComplete;
    public bool capturesComplete;
    public List<BackendStatisticsEntry> entries;
}

[Serializable]
public sealed class BackendStatisticsEntry
{
    public string id;
    public int moveNumber;
    public string actor;
    public string text;
    public bool isMove;
    public string captured;
}
