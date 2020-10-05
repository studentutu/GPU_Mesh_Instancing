using Morpeh;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

[Il2CppSetOption(Option.NullChecks, false)]
[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
[Il2CppSetOption(Option.DivideByZeroChecks, false)]
[CreateAssetMenu(menuName = "ECS/Systems/" + nameof(MeshAnimatorSystem))]
public sealed class MeshAnimatorSystem : UpdateSystem
{
    public override void OnAwake()
    {
    }

    public override void OnUpdate(float deltaTime)
    {
    }
}