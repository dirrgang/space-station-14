// Cosmatic Drift database models live outside of Model.cs to keep upstream changes minimal.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

public static class CDModel
{
    /// <summary>
    /// Stores Cosmatic Drift-specific character data separately from the main profile row. EF Core migrations
    /// require this table to be optional, so callers must tolerate the profile being absent.
    /// </summary>
    public class CDProfile
    {
        public int Id { get; set; }

        public int ProfileId { get; set; }
        public Profile Profile { get; set; } = null!;

        public float Height { get; set; } = 1f;

        [Column("character_records", TypeName = "jsonb")]
        public JsonDocument? CharacterRecords { get; set; }

        public List<CharacterRecordEntry> CharacterRecordEntries { get; set; } = new();
    }

    public enum DbRecordEntryType : byte
    {
        Medical = 0,
        Security = 1,
        Employment = 2,
        Admin = 3,
    }

    [Table("cd_character_record_entries"), Index(nameof(Id))]
    public sealed class CharacterRecordEntry
    {
        public int Id { get; set; }

        public string Title { get; set; } = null!;

        public string Involved { get; set; } = null!;

        public string Description { get; set; } = null!;

        public DbRecordEntryType Type { get; set; }

        public int CDProfileId { get; set; }
        public CDProfile CDProfile { get; set; } = null!;
    }
}
