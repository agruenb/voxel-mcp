namespace VoxelKing.Core.Tests.Persistence;

using System.Text.Json.Nodes;
using VoxelKing.Core.Persistence;
using Xunit;

public sealed class SaveMigrationTests
{
    private sealed class V0ToV1Migration : ISaveMigration
    {
        public int FromVersion => 0;
        public int ToVersion => 1;

        public void MigrateWorld(JsonObject worldJson)
        {
            worldJson["migratedWorld"] = true;
        }

        public void MigrateNpcs(JsonObject npcsJson)
        {
            npcsJson["migratedNpcs"] = true;
        }

        public void MigratePlayer(JsonObject playerJson)
        {
            playerJson["migratedPlayer"] = true;
        }
    }

    [Fact]
    public void SaveMigrationManager_UpgradesLegacyVersion0Json()
    {
        var manager = new SaveMigrationManager();
        manager.RegisterMigration(new V0ToV1Migration());

        string legacyWorld = "{\"seed\": 42, \"worldName\": \"oldWorld\"}";
        string migratedWorld = manager.MigrateWorldJson(legacyWorld);

        var node = JsonNode.Parse(migratedWorld)?.AsObject();
        Assert.NotNull(node);
        Assert.Equal(1, (int)node["version"]!);
        Assert.True((bool)node["migratedWorld"]!);

        string legacyNpcs = "{\"npcs\": []}";
        string migratedNpcs = manager.MigrateNpcsJson(legacyNpcs);
        var npcsNode = JsonNode.Parse(migratedNpcs)?.AsObject();
        Assert.NotNull(npcsNode);
        Assert.Equal(1, (int)npcsNode["version"]!);
        Assert.True((bool)npcsNode["migratedNpcs"]!);
    }
}
