namespace VoxelKing.Core.Persistence;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Defines a migration step between save format versions.
/// </summary>
public interface ISaveMigration
{
    int FromVersion { get; }
    int ToVersion { get; }

    void MigrateWorld(JsonObject worldJson);
    void MigrateNpcs(JsonObject npcsJson);
    void MigratePlayer(JsonObject playerJson);
}

/// <summary>
/// Manages format migration hooks for upgrading older save files to current versions.
/// </summary>
public sealed class SaveMigrationManager
{
    public const int TargetVersion = 1;

    private readonly List<ISaveMigration> _migrations = new();

    public IReadOnlyList<ISaveMigration> Migrations => _migrations;

    public void RegisterMigration(ISaveMigration migration)
    {
        ArgumentNullException.ThrowIfNull(migration);
        _migrations.Add(migration);
        _migrations.Sort((a, b) => a.FromVersion.CompareTo(b.FromVersion));
    }

    public string MigrateWorldJson(string json)
    {
        return MigrateJson(json, (migration, obj) => migration.MigrateWorld(obj));
    }

    public string MigrateNpcsJson(string json)
    {
        return MigrateJson(json, (migration, obj) => migration.MigrateNpcs(obj));
    }

    public string MigratePlayerJson(string json)
    {
        return MigrateJson(json, (migration, obj) => migration.MigratePlayer(obj));
    }

    private string MigrateJson(string json, Action<ISaveMigration, JsonObject> applyMigration)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return json;
        }

        var node = JsonNode.Parse(json);
        if (node is not JsonObject rootObj)
        {
            return json;
        }

        int currentVersion = rootObj.TryGetPropertyValue("version", out var verNode) && verNode != null
            ? (int)verNode
            : 0;

        while (currentVersion < TargetVersion)
        {
            var migration = _migrations.Find(m => m.FromVersion == currentVersion);
            if (migration == null)
            {
                // No migration needed or step missing; break
                break;
            }

            applyMigration(migration, rootObj);
            currentVersion = migration.ToVersion;
            rootObj["version"] = currentVersion;
        }

        return rootObj.ToJsonString(PersistenceJsonOptions.Default);
    }
}
