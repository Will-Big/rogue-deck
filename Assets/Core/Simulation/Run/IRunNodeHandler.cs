namespace FateWeaver.Simulation.Run
{
    /// <summary>A run-map node type handler. New node types extend the run by adding one handler
    /// and registering its key (AGENTS.md rule 9) — never by growing a central switch.
    /// Concrete handlers expose their own entry points (an instant Resolve, or a combat
    /// CreateSession/ApplyResult pair); callers resolve by key and use the concrete type.</summary>
    public interface IRunNodeHandler
    {
        RunNodeKey Key { get; }
    }
}
