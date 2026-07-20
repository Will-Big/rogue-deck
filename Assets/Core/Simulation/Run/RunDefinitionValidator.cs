using System.Collections.Generic;

namespace FateWeaver.Simulation.Run
{
    /// <summary>Boot-time validation (AGENTS.md rule 9): every authored node needs a registered
    /// handler and a payload. Returns an empty list when the definition is valid.</summary>
    public static class RunDefinitionValidator
    {
        public static IReadOnlyList<string> Validate(RunDefinition definition, RunNodeRegistry registry)
        {
            var errors = new List<string>();
            for (int i = 0; i < definition.Nodes.Count; i++)
            {
                var node = definition.Nodes[i];
                if (!registry.Contains(node.Key))
                {
                    errors.Add($"node[{i}]: no handler registered for key '{node.Key}'");
                }

                if (node.Payload == null)
                {
                    errors.Add($"node[{i}] ('{node.Key}'): payload is null");
                }
            }

            return errors;
        }
    }
}
