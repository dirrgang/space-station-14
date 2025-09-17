using System;
using System.Collections.Generic;
using Content.Server.Forensics;
using Content.Server.StationRecords;
using Content.Server.StationRecords.Systems;
using Content.Shared.Forensics.Components;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Content.Shared.Roles;
using Content.Shared.StationRecords;
using Content.Shared._CD.Records;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;

namespace Content.Server._CD.Records;

public sealed class CharacterRecordsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly StationRecordsSystem _stationRecords = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn, after: [typeof(StationRecordsSystem)]);
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent args)
    {
        if (!HasComp<StationRecordsComponent>(args.Station))
        {
            Log.Error("Tried to add CharacterRecords on a station without StationRecords");
            return;
        }

        if (!HasComp<CharacterRecordsComponent>(args.Station))
            AddComp<CharacterRecordsComponent>(args.Station);

        if (string.IsNullOrEmpty(args.JobId))
        {
            Log.Error($"Null JobId in CharacterRecordsSystem::OnPlayerSpawn for character {args.Profile.Name} played by {args.Player.Name}");
            return;
        }

        if (HasComp<SkipLoadingCharacterRecordsComponent>(args.Mob))
            return;

        var profile = args.Profile;
        if (profile.CDCharacterRecords == null)
        {
            Log.Error($"Null records in CharacterRecordsSystem::OnPlayerSpawn for character {args.Profile.Name} played by {args.Player.Name}.");
            return;
        }

        var player = args.Mob;

        if (!_prototype.TryIndex(args.JobId, out JobPrototype? jobPrototype))
        {
            throw new ArgumentException($"Invalid job prototype ID: {args.JobId}");
        }

        TryComp(player, out FingerprintComponent? fingerprintComponent);
        TryComp(player, out DnaComponent? dnaComponent);

        var jobTitle = jobPrototype.LocalizedName;
        var stationRecordsKey = FindStationRecordsKey(player);

        if (stationRecordsKey != null &&
            _stationRecords.TryGetRecord<GeneralStationRecord>(stationRecordsKey.Value, out var stationRecords))
        {
            jobTitle = stationRecords.JobTitle;
        }

        var records = new FullCharacterRecords(
            pRecords: new PlayerProvidedCharacterRecords(profile.CDCharacterRecords),
            stationRecordsKey: stationRecordsKey?.Id,
            name: profile.Name,
            age: profile.Age,
            species: profile.Species,
            jobTitle: jobTitle,
            jobIcon: jobPrototype.Icon,
            gender: profile.Gender,
            sex: profile.Sex,
            fingerprint: fingerprintComponent?.Fingerprint,
            dna: dnaComponent?.DNA,
            owner: player);

        AddRecord(args.Station, args.Mob, records);
    }

    private StationRecordKey? FindStationRecordsKey(EntityUid uid)
    {
        if (!_inventory.TryGetSlotEntity(uid, "id", out var idUid))
            return null;

        var keyStorageEntity = idUid;
        if (TryComp<PdaComponent>(idUid, out var pda) && pda.ContainedId is { } containedId)
        {
            keyStorageEntity = containedId;
        }

        if (!TryComp<StationRecordKeyStorageComponent>(keyStorageEntity, out var storage))
            return null;

        return storage.Key;
    }

    private void AddRecord(EntityUid station, EntityUid player, FullCharacterRecords records, CharacterRecordsComponent? recordsDb = null)
    {
        if (!Resolve(station, ref recordsDb))
            return;

        var key = recordsDb.CreateNewKey();
        recordsDb.Records.Add(key, records);
        var playerKey = new CharacterRecordKey { Station = station, Index = key };
        AddComp(player, new CharacterRecordKeyStorageComponent(playerKey));

        RaiseLocalEvent(station, new CharacterRecordsModifiedEvent());
    }

    public void DelEntry(
        EntityUid station,
        EntityUid player,
        CharacterRecordType type,
        int index,
        CharacterRecordsComponent? recordsDb = null,
        CharacterRecordKeyStorageComponent? key = null)
    {
        if (!Resolve(station, ref recordsDb) || !Resolve(player, ref key))
            return;

        if (!recordsDb.Records.TryGetValue(key.Key.Index, out var value))
            return;

        var records = value.PRecords;
        List<PlayerProvidedCharacterRecords.RecordEntry>? list = null;

        switch (type)
        {
            case CharacterRecordType.Employment:
                list = records.EmploymentEntries;
                break;
            case CharacterRecordType.Medical:
                list = records.MedicalEntries;
                break;
            case CharacterRecordType.Security:
                list = records.SecurityEntries;
                break;
            case CharacterRecordType.Admin:
                list = records.AdminEntries;
                break;
            default:
                Log.Warning($"Attempted to remove unsupported record type {type} for entity {player}");
                return;
        }

        if (list == null)
            return;

        if (index < 0 || index >= list.Count)
        {
            Log.Warning($"Attempted to remove {type} record entry at invalid index {index} for entity {player}");
            return;
        }

        list.RemoveAt(index);

        RaiseLocalEvent(station, new CharacterRecordsModifiedEvent());
    }

    public void ResetRecord(
        EntityUid station,
        EntityUid player,
        CharacterRecordsComponent? recordsDb = null,
        CharacterRecordKeyStorageComponent? key = null)
    {
        if (!Resolve(station, ref recordsDb) || !Resolve(player, ref key))
            return;

        if (!recordsDb.Records.TryGetValue(key.Key.Index, out var value))
            return;

        var records = PlayerProvidedCharacterRecords.DefaultRecords();
        if (TryComp(player, out MetaDataComponent? meta))
            value.Name = meta.EntityName;

        value.PRecords = records;
        RaiseLocalEvent(station, new CharacterRecordsModifiedEvent());
    }

    public void DeleteAllRecords(EntityUid player, CharacterRecordKeyStorageComponent? key = null)
    {
        if (!Resolve(player, ref key))
            return;

        var station = key.Key.Station;
        CharacterRecordsComponent? records = null;
        if (!Resolve(station, ref records))
            return;

        if (records.Records.Remove(key.Key.Index))
            RaiseLocalEvent(station, new CharacterRecordsModifiedEvent());
    }

    public IDictionary<uint, FullCharacterRecords> QueryRecords(EntityUid station, CharacterRecordsComponent? recordsDb = null)
    {
        return !Resolve(station, ref recordsDb)
            ? new Dictionary<uint, FullCharacterRecords>()
            : recordsDb.Records;
    }
}

public sealed class CharacterRecordsModifiedEvent : EntityEventArgs;
