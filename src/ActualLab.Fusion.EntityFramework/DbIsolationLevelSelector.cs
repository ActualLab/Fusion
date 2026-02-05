using System.Data;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework;

/// <summary>
/// A delegate that selects the <see cref="IsolationLevel"/> for a given <see cref="CommandContext"/>.
/// </summary>
public delegate IsolationLevel DbIsolationLevelSelector(CommandContext commandContext);

/// <summary>
/// A delegate that selects the <see cref="IsolationLevel"/> for a given <see cref="CommandContext"/>
/// scoped to a specific <see cref="DbContext"/> type.
/// </summary>
public delegate IsolationLevel DbIsolationLevelSelector<TDbContext>(CommandContext commandContext)
    where TDbContext : DbContext;
