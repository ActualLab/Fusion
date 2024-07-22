using System.Data;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework;

public delegate IsolationLevel DbIsolationLevelSelector(CommandContext commandContext);
public delegate IsolationLevel DbIsolationLevelSelector<TDbContext>(CommandContext commandContext)
    where TDbContext : DbContext;
