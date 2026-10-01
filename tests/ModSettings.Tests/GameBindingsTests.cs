namespace ModSettings.Tests;

public class GameBindingsTests
{
    // Shaped like InputActionAsset.ToJson().
    private const string Asset = """
        {
          "name": "Main",
          "maps": [
            {
              "name": "Player",
              "actions": [ { "name": "Jump" }, { "name": "Move" } ],
              "bindings": [
                { "name": "", "id": "j1", "path": "<Keyboard>/space", "action": "Jump", "isComposite": false, "isPartOfComposite": false },
                { "name": "", "id": "j2", "path": "<Gamepad>/buttonSouth", "action": "Jump", "isComposite": false, "isPartOfComposite": false },
                { "name": "WASD", "id": "m0", "path": "2DVector", "action": "Move", "isComposite": true, "isPartOfComposite": false },
                { "name": "up", "id": "m1", "path": "<Keyboard>/w", "action": "Move", "isComposite": false, "isPartOfComposite": true },
                { "name": "", "id": "b1", "path": "<Keyboard>/b", "action": "Build", "isComposite": false, "isPartOfComposite": false }
              ]
            }
          ]
        }
        """;

    // Shaped like InputActionAsset.SaveBindingOverridesAsJson().
    private const string Overrides = """
        { "bindings": [
            { "action": "Player/Jump", "id": "j1", "path": "<Keyboard>/k", "interactions": "", "processors": "" },
            { "action": "Player/Build", "id": "b1", "path": "", "interactions": "", "processors": "" }
        ] }
        """;

    [Fact]
    public void ReadsKeyboardBindingsWithOverrides()
    {
        var bindings = GameBindings.FromJson(new[] { Asset }, Overrides);

        Assert.Equal(new[] { "Game: Player/Jump", "Game: Player/Move (up)" }, bindings.Select(b => b.Label));
        Assert.Equal(new[] { "K" }, bindings[0].Keys);
        Assert.Equal(new[] { "W" }, bindings[1].Keys);
        Assert.All(bindings, b => Assert.True(b.FromGame));
    }

    [Fact]
    public void WorksWithoutOverrides()
    {
        var bindings = GameBindings.FromJson(new[] { Asset }, null);
        Assert.Equal(new[] { "Space" }, bindings[0].Keys);
        Assert.Equal(3, bindings.Count);
    }

    [Fact]
    public void BrokenJsonGivesNothing() => Assert.Empty(GameBindings.FromJson(new[] { "{not json" }, "also not"));
}
