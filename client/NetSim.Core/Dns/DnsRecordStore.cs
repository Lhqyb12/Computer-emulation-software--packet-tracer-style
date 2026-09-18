using NetSim.Core.Common.Exceptions;

namespace NetSim.Core.Dns;

/// <summary>Default <see cref="IDnsRecordStore"/>. A plain in-memory table - no protocol logic, no events (<see cref="DnsServer"/> raises those).</summary>
public sealed class DnsRecordStore : IDnsRecordStore
{
    private readonly Dictionary<(DomainName Name, DnsRecordType Type), List<DnsRecord>> _records = [];

    public void AddRecord(DnsRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var key = (record.Name, record.Type);
        if (!_records.TryGetValue(key, out var list))
        {
            list = [];
            _records[key] = list;
        }

        if (list.Contains(record))
        {
            throw new DomainException($"A duplicate {record.Type} record for '{record.Name}' already exists.");
        }

        list.Add(record);
    }

    public bool RemoveRecord(DnsRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var key = (record.Name, record.Type);
        if (!_records.TryGetValue(key, out var list) || !list.Remove(record))
        {
            return false;
        }

        if (list.Count == 0)
        {
            _records.Remove(key);
        }

        return true;
    }

    public int RemoveRecords(DomainName name, DnsRecordType? type = null)
    {
        if (type is { } exactType)
        {
            var key = (name, exactType);
            if (!_records.TryGetValue(key, out var list))
            {
                return 0;
            }

            _records.Remove(key);
            return list.Count;
        }

        var matchingKeys = _records.Keys.Where(k => k.Name == name).ToList();
        var removed = 0;
        foreach (var key in matchingKeys)
        {
            removed += _records[key].Count;
            _records.Remove(key);
        }

        return removed;
    }

    public IReadOnlyList<DnsRecord> FindRecords(DomainName name, DnsRecordType type) =>
        _records.TryGetValue((name, type), out var list) ? list.AsReadOnly() : Array.Empty<DnsRecord>();

    public IReadOnlyList<DnsRecord> FindByName(DomainName name) =>
        _records.Where(pair => pair.Key.Name == name).SelectMany(pair => pair.Value).ToList();

    public bool ContainsName(DomainName name) => _records.Keys.Any(k => k.Name == name);

    public void Clear() => _records.Clear();

    public IReadOnlyCollection<DnsRecord> AllRecords => _records.Values.SelectMany(list => list).ToList();
}
