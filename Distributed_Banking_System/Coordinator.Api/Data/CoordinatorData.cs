using System.Collections.Concurrent;
using Coordinator.Api.Models;

namespace Coordinator.Api.Data;

public class CoordinatorData
{
    public ConcurrentDictionary<string, TxnRecord> Transactions { get; } = new();
}