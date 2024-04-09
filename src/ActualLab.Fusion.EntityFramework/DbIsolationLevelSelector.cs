using System.Data;

namespace ActualLab.Fusion.EntityFramework;

public delegate IsolationLevel DbIsolationLevelSelector(CommandContext commandContext);
public delegate IsolationLevel DbIsolationLevelSelector<TDbContext>(CommandContext commandContext);
