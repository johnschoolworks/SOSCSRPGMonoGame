using Tutorial016.World;

namespace Tutorial016.Dialogue;

public sealed record DialogueChoice(int Id, string Text);
public sealed record NpcDefinition(int Id, string Name, int LocationId, string Greeting,
    IReadOnlyList<DialogueChoice> Choices);

public static class NpcFactory
{
    public const int WeaverId = 1;
    private static readonly NpcDefinition Weaver = new(
        WeaverId, "Elena the Weaver", WorldFactory.TownSquareId,
        "Welcome, traveler. The forest spiders have silk I need.",
        new[] {
            new DialogueChoice(1, "Ask about the Spider Silk quest"),
            new DialogueChoice(2, "Offer collected Spider Silk"),
            new DialogueChoice(3, "Say goodbye")
        });

    public static IReadOnlyList<NpcDefinition> AtLocation(int locationId) =>
        locationId == Weaver.LocationId ? new[] { Weaver } : Array.Empty<NpcDefinition>();

    public static NpcDefinition? FindAtLocation(int npcId, int locationId) =>
        npcId == Weaver.Id && locationId == Weaver.LocationId ? Weaver : null;
}
