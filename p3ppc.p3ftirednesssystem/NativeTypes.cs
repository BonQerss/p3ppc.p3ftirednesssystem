using System.Runtime.InteropServices;

namespace p3ppc.p3ftirednesssystem;

internal static unsafe class NativeTypes
{
    [StructLayout(LayoutKind.Explicit)]
    internal struct BattleUnit
    {
        [FieldOffset(0x00)]
        internal ushort Type;

        [FieldOffset(0x08)]
        internal PartyMemberInfo* MemberInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct PartyMemberInfo
    {
        [FieldOffset(0x02)]
        internal ushort MemberId;
    }

    

    [StructLayout(LayoutKind.Explicit)]
    internal struct CombatModel
    {

        [FieldOffset(0xCE0)]
        internal PartyMemberInfo* MemberInfo;

        [FieldOffset(0xCE8)]
        internal CombatModel* Next;
    }

    
    
    [StructLayout(LayoutKind.Explicit)]
    internal struct BattleActor
    {
        [FieldOffset(0x00)]
        internal ulong OwnerToken;

        [FieldOffset(0x38)]
        internal CombatModel* Model;

        [FieldOffset(0x570)]
        internal BattleActor* Next;
    }

    

    [StructLayout(LayoutKind.Explicit)]
    internal struct BattleTask
    {
        [FieldOffset(0x00)]
        internal byte DependencyType;

        [FieldOffset(0x08)]
        internal ulong DependencyTaskId;

        [FieldOffset(0x10)]
        internal byte DependencyType2;

        [FieldOffset(0x18)]
        internal ulong DependencyTaskId2;

        
        [FieldOffset(0x20)]
        internal byte CompletionDependencyType;

        [FieldOffset(0x28)]
        internal ulong CompletionDependencyTaskId;

        [FieldOffset(0x30)]
        internal byte CompletionDependencyType2;

        [FieldOffset(0x38)]
        internal ulong CompletionDependencyTaskId2;

        [FieldOffset(0x48)]
        internal float Delay;

        [FieldOffset(0x58)]
        internal ulong TaskId;

        [FieldOffset(0x60)]
        internal ulong OwnerToken;

        [FieldOffset(0x70)]
        internal nint Callback;

        [FieldOffset(0x90)]
        internal BattleTask* Next;
    }
}
