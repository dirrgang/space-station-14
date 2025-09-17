using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using Content.Server.Database;
using Content.Shared._CD.Records;

namespace Content.Server._CD.Records;

/// <summary>
/// Helpers for translating Cosmatic Drift character record data to and from EF models.
/// </summary>
public static class RecordsSerialization
{
    private static int DeserializeInt(JsonElement element, string key, int def)
    {
        if (element.TryGetProperty(key, out var prop) && prop.TryGetInt32(out var value))
            return value;

        return def;
    }

    private static bool DeserializeBool(JsonElement element, string key, bool def)
    {
        if (!element.TryGetProperty(key, out var prop))
            return def;

        return prop.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => def,
        };
    }

    [return: NotNullIfNotNull(nameof(def))]
    private static string? DeserializeString(JsonElement element, string key, string? def)
    {
        if (!element.TryGetProperty(key, out var prop))
            return def;

        if (prop.ValueKind == JsonValueKind.String)
            return prop.GetString() ?? def;

        return def;
    }

    private static List<PlayerProvidedCharacterRecords.RecordEntry> DeserializeEntries(
        List<CDModel.CharacterRecordEntry> entries,
        CDModel.DbRecordEntryType type)
    {
        return entries.Where(e => e.Type == type)
            .OrderBy(e => e.Id)
            .Select(e => new PlayerProvidedCharacterRecords.RecordEntry(e.Title, e.Involved, e.Description))
            .ToList();
    }

    /// <summary>
    /// Manual deserializer for player-authored record data that tolerates missing or extra fields.
    /// </summary>
    public static PlayerProvidedCharacterRecords Deserialize(
        JsonDocument json,
        List<CDModel.CharacterRecordEntry> entries)
    {
        var element = json.RootElement;
        var def = PlayerProvidedCharacterRecords.DefaultRecords();
        return new PlayerProvidedCharacterRecords(
            height: DeserializeInt(element, nameof(def.Height), def.Height),
            weight: DeserializeInt(element, nameof(def.Weight), def.Weight),
            emergencyContactName: DeserializeString(element, nameof(def.EmergencyContactName), def.EmergencyContactName),
            hasWorkAuthorization: DeserializeBool(element, nameof(def.HasWorkAuthorization), def.HasWorkAuthorization),
            identifyingFeatures: DeserializeString(element, nameof(def.IdentifyingFeatures), def.IdentifyingFeatures),
            allergies: DeserializeString(element, nameof(def.Allergies), def.Allergies),
            drugAllergies: DeserializeString(element, nameof(def.DrugAllergies), def.DrugAllergies),
            postmortemInstructions: DeserializeString(element, nameof(def.PostmortemInstructions), def.PostmortemInstructions),
            medicalEntries: DeserializeEntries(entries, CDModel.DbRecordEntryType.Medical),
            securityEntries: DeserializeEntries(entries, CDModel.DbRecordEntryType.Security),
            employmentEntries: DeserializeEntries(entries, CDModel.DbRecordEntryType.Employment));
    }

    private static CDModel.CharacterRecordEntry ConvertEntry(
        PlayerProvidedCharacterRecords.RecordEntry entry,
        CDModel.DbRecordEntryType type)
    {
        entry.EnsureValid();
        return new CDModel.CharacterRecordEntry
        {
            Title = entry.Title,
            Involved = entry.Involved,
            Description = entry.Description,
            Type = type,
        };
    }

    public static List<CDModel.CharacterRecordEntry> GetEntries(PlayerProvidedCharacterRecords records)
    {
        return records.MedicalEntries.Select(medical => ConvertEntry(medical, CDModel.DbRecordEntryType.Medical))
            .Concat(records.SecurityEntries.Select(security => ConvertEntry(security, CDModel.DbRecordEntryType.Security)))
            .Concat(records.EmploymentEntries.Select(employment => ConvertEntry(employment, CDModel.DbRecordEntryType.Employment)))
            .ToList();
    }
}
