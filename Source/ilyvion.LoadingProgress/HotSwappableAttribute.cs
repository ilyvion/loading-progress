namespace ilyvion.LoadingProgress;

// Allows using ilyvion.hotswap with this mod
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
internal sealed class HotSwappableAttribute : Attribute
{
}
