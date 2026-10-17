namespace IdleCrpg.Core;

/// <summary>可持久化的掉落随机源；读档后续随机序列保持一致，不重新掷已有奖励。</summary>
public sealed class PersistentRandom
{
    public uint State { get; set; }
    public PersistentRandom(uint state) { State=state==0?0x6D2B79F5u:state; }
    private uint NextValue() {uint x=State;x^=x<<13;x^=x>>17;x^=x<<5;return State=x;}
    public double NextDouble() => NextValue()/4294967296.0;
    public int Next(int min,int max) => min+(int)(NextDouble()*(max-min));
}
