using System;
using FateWeaver.Simulation;

namespace FateWeaver.Headless
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var id = args.Length > 0 ? args[0] : "quick-cut-swap";
            Console.Write(ScenarioCliReport.Build(id));
        }
    }
}
