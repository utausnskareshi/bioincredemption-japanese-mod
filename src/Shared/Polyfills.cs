// netstandard2.1 には record / init アクセサが要求する IsExternalInit 型が無いため、同名の型を自前で用意する。
// コンパイラが参照するだけのマーカー型で、実行時の動作には影響しない。
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
