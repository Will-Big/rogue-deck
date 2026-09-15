namespace FateWeaver.Simulation.Run
{
    /// <summary>노드 안에서 쓰는 무작위의 목적. 값은 스트림 이름표이며 저장 형식처럼 굳는다 —
    /// 바꾸면 모든 기존 시드의 결과가 바뀐다. string.GetHashCode는 프로세스마다 무작위화되므로
    /// 이름표를 문자열에서 만들지 않고 고정 상수로 둔다.</summary>
    public enum SeedStream : ulong
    {
        Encounter = 0x454E43,
        Combat = 0x434D42,
        Reward = 0x524557
    }

    /// <summary>런 시드에서 노드 시드를, 노드 시드에서 목적별 스트림 시드를 만든다(설계 결정 4).
    ///
    /// 스트림은 뽑는 순서가 아니라 이름표로 파생된다. 그래서 어느 스트림을 먼저 쓰든·안 쓰든 다른
    /// 스트림의 값이 변하지 않는다. seed + index 같은 선형 파생은 인접 시드로 초기화한 Random들의
    /// 첫 출력이 상관되므로(Slay the Spire 2 보고) SplitMix64 finalizer로 섞는다.</summary>
    public static class SeedDerivation
    {
        public static int NodeSeed(int runSeed, int nodeIndex)
            => Mix(runSeed, unchecked((ulong)nodeIndex + 1UL));

        public static int Stream(int nodeSeed, SeedStream stream)
            => Mix(nodeSeed, (ulong)stream);

        private static int Mix(int parent, ulong tag)
        {
            unchecked
            {
                ulong x = ((ulong)(uint)parent * 0x9E3779B97F4A7C15UL) ^ tag;
                x ^= x >> 30;
                x *= 0xBF58476D1CE4E5B9UL;
                x ^= x >> 27;
                x *= 0x94D049BB133111EBUL;
                x ^= x >> 31;
                return (int)(uint)x;
            }
        }
    }
}
