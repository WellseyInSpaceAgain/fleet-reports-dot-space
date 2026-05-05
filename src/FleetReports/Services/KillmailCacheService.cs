using FleetReports.Models;
using LiteDB;

namespace FleetReports.Services;

public class KillmailCacheService(LiteDatabase db, ISystemNameCacheService systemNames) : IKillmailCacheService
{
    private readonly ILiteCollection<KillmailDocument> _collection = db.GetCollection<KillmailDocument>("killmails");
    public async Task<KillmailDocument?> GetAsync(int killmailId)
    {
        var document = _collection.FindById(killmailId);
        return document;
    }

    public async Task<KillmailDocument> UpsertAsync(EsiKillmail killmail, string hash, decimal totalValue)
    {
        var topDamage = killmail.Attackers
            .Where(x => x.CharacterId.HasValue)
            .OrderByDescending(x => x.DamageDone)
            .FirstOrDefault();

        var finalBlow = killmail.Attackers.FirstOrDefault(x => x.FinalBlow && x.CharacterId.HasValue);

        var systemName = await systemNames.GetOrResolveAsync(killmail.SolarSystemId);

        var document = new KillmailDocument
        {
            Id = killmail.KillmailId,
            Hash = hash,
            KillmailTime = killmail.KillmailTime,
            SystemName = systemName,
            ShipTypeId = killmail.Victim.ShipTypeId,
            VictimId = killmail.Victim.CharacterId,
            TopDamageId = topDamage?.CharacterId,
            FinalBlowId = finalBlow?.CharacterId,
            TotalValue = totalValue
        };

        _collection.Upsert(document);
        return document;
    }
}
