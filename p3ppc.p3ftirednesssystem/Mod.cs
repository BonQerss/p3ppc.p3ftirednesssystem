using Reloaded.Hooks.Definitions;
using Reloaded.Mod.Interfaces;
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;
using p3ppc.p3ftirednesssystem.Template;
using System.Runtime.InteropServices;
using static p3ppc.p3ftirednesssystem.NativeTypes;

namespace p3ppc.p3ftirednesssystem;

public unsafe class Mod : ModBase
{
    private const uint SpecialOperationFlag = 0x172;

    private const uint FirstPresenceFlag = 322;
    private const uint LastPresenceFlag = 329;
    private const uint FirstAvailabilityFlag = 3096;
    private const uint MemberReturnTriggerFlag = 3568;

    
    private static readonly ushort[] DepartureMembers = { 2, 3, 4, 5, 7, 8, 9, 10 };

    private const ushort Tired = 3;
    private const ushort Sick = 5;
    private const int MaxFatigueDrainMultiplier = 20;

    private readonly IReloadedHooks _hooks = null!;
    private Config _config = null!;

    private IHook<FatigueCallback>? _fatigueHook;
    private IHook<VictoryPresentationInit>? _victoryPresentationHook;
    private IHook<BitSet>? _bitSetHook;

    private FatigueCallback? _fatigueOriginal;
    private VictoryPresentationInit? _victoryPresentationOriginal;
    private BitSet? _bitSetOriginal;

    private AdjustEndurance? _adjustEndurance;
    private GetEndurance? _getEndurance;
    private GetCondition? _getCondition;
    private SetUnitCondition? _setUnitCondition;
    private IsCharacterDead? _isCharacterDead;
    private BitCheck? _bitCheck;
    private RandInt? _randInt;
    private IncrementBattleCounter? _incrementBattleCounter;
    private GetBattleCounter? _getBattleCounter;

    private TirednessPresentation.CreateBattleStateTask? _createBattleStateTask;
    private TirednessPresentation.CreateCrossfadeTask? _createCrossfadeTask;
    private TirednessPresentation.CreateMotionTask? _createMotionTask;
    private TirednessPresentation.CreateSoundTask? _createSoundTask;
    private TirednessPresentation.CreateActorVoiceTask? _createActorVoiceTask;
    private TirednessPresentation.CreateNavigatorTask? _createNavigatorTask;
    private TirednessPresentation.EnqueueBattleTask? _enqueueBattleTask;
    private TirednessPresentation? _presentation;

    private nint _fatigueAddress;
    private nint _victoryPresentationAddress;
    private nint _combatInfoAnchorAddress;
    private nint _allDeadAddress;
    private nint _setUnitConditionAddress;
    private nint _counterIncrementAddress;
    private nint _counterGetAddress;
    private nint _bitSetThunkAddress;
    private nint _randIntAddress;
    private nint _combatInfoGlobalAddress;

    private int _resolvedPieces;
    private const int RequiredPieces = 12;
    private bool _installed;

    public Mod(ModContext context)
    {
        _hooks = context.Hooks ?? throw new InvalidOperationException("Reloaded.Hooks controller is unavailable.");
        _config = context.Configuration;

        if (!Utils.Initialise(context.Logger, _config, context.ModLoader))
            return;

        ScanNativeFunctions();
    }

    public Mod() { }

    public override void ConfigurationUpdated(Config configuration)
    {
        _config = configuration;
        Utils.UpdateConfig(configuration);
        Utils.Log("Configuration updated.");
    }

    private void ScanNativeFunctions()
    {
        
        Utils.SigScan("Battle fatigue callback", "40 53 48 83 EC 20 48 8B D9 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 66 83 3B 00 75 ?? 48 8B 43 08 BA FF FF FF FF", address =>
            {
                _fatigueAddress = address;

                

                _adjustEndurance = Marshal.GetDelegateForFunctionPointer<AdjustEndurance>(Utils.ResolveRelativeCall(address + 0x2B));
                _getEndurance = Marshal.GetDelegateForFunctionPointer<GetEndurance>(Utils.ResolveRelativeCall(address + 0x33));
                _getCondition = Marshal.GetDelegateForFunctionPointer<GetCondition>(Utils.ResolveRelativeCall(address + 0x40));

                PieceResolved();
            });

        
        Utils.SigScan("Battle fatigue flag helper", "48 8B 75 48 33 FF 39 7E 10 0F 86 ?? ?? ?? ?? 48 89 5C 24 30", address =>
            {
                
                _bitCheck = Marshal.GetDelegateForFunctionPointer<BitCheck>(Utils.ResolveRelativeCall(address + 0x37));
                PieceResolved();
            });

        
        Utils.SigScan("Battle unit dead-state helper", "48 89 5C 24 08 57 48 83 EC 20 48 8B F9 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 66 83 3F 01", address =>
            {
                _allDeadAddress = address;
                _isCharacterDead = Marshal.GetDelegateForFunctionPointer<IsCharacterDead>(Utils.ResolveRelativeCall(address + 0x44));
                PieceResolved();
            });

        
        Utils.SigScan("Set battle unit condition", "48 89 5C 24 08 57 48 83 EC 20 48 8B D9 0F B7 FA 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? F6 03 04 75 0C 0F B7 4B 02 0F B7 D7 E8 ?? ?? ?? ?? 48 8B 5C 24 30", address =>
            {
                _setUnitConditionAddress = address;
                _setUnitCondition = Marshal.GetDelegateForFunctionPointer<SetUnitCondition>(address);
                PieceResolved();
            });

        Utils.SigScan("Increment FES battle condition counter", "48 83 EC 28 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? FF 05 ?? ?? ?? ?? 48 83 C4 28 C3", address =>
            {
                _counterIncrementAddress = address;
                _incrementBattleCounter = Marshal.GetDelegateForFunctionPointer<IncrementBattleCounter>(address);
                PieceResolved();
            });

        Utils.SigScan("FES battle condition counter getter caller", "48 83 EC 28 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? E8 ?? ?? ?? ?? 45 33 C0 48 8D 0D ?? ?? ?? ?? 90", address =>
            {
                _counterGetAddress = Utils.ResolveRelativeCall(address + 0x10);
                _getBattleCounter = Marshal.GetDelegateForFunctionPointer<GetBattleCounter>(_counterGetAddress);
                PieceResolved();
            });

        
        
        Utils.SigScan("RandInt caller", "40 53 48 83 EC 40 48 8B D9 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 33 DB 41 BB C0 00 00 00", address =>
            {
                _randIntAddress = Utils.ResolveRelativeCall(address + 0x73);
                _randInt = Marshal.GetDelegateForFunctionPointer<RandInt>(_randIntAddress);
                PieceResolved();
            });

        
        Utils.SigScan("BitSet call site", "BA 01 00 00 00 B9 1D 14 00 00 E8 ?? ?? ?? ?? E8 ?? ?? ?? ??", address =>
            {
                _bitSetThunkAddress = Utils.ResolveRelativeCall(address + 0x0A);
                PieceResolved();
            });

        
        
        Utils.SigScan("COMBAT_INFO result-state anchor", "40 53 48 83 EC 20 48 8B D9 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 BA FF FF FF FF FF FF FF 3F", address =>
            {
                _combatInfoAnchorAddress = address;

                
                _combatInfoGlobalAddress = (nint)Utils.GetGlobalAddress((nuint)(address + 0x18));
                PieceResolved();
            });

        
        Utils.SigScan("Victory presentation task family", "48 8B C4 48 89 58 10 48 89 68 18 48 89 70 20 57 41 54 41 55 41 56 41 57 48 81 EC 90 00 00 00 0F 29 70 C8", address =>
            {
                _victoryPresentationAddress = address;

                _enqueueBattleTask = Marshal.GetDelegateForFunctionPointer<TirednessPresentation.EnqueueBattleTask>(Utils.ResolveRelativeCall(address + 0x1AA));
                _createMotionTask = Marshal.GetDelegateForFunctionPointer<TirednessPresentation.CreateMotionTask>(Utils.ResolveRelativeCall(address + 0x1D5));
                _createActorVoiceTask = Marshal.GetDelegateForFunctionPointer<TirednessPresentation.CreateActorVoiceTask>(Utils.ResolveRelativeCall(address + 0x3EF));
                _createBattleStateTask = Marshal.GetDelegateForFunctionPointer<TirednessPresentation.CreateBattleStateTask>(Utils.ResolveRelativeCall(address + 0x4F4));
                _createNavigatorTask = Marshal.GetDelegateForFunctionPointer<TirednessPresentation.CreateNavigatorTask>(Utils.ResolveRelativeCall(address + 0x52B));
                PieceResolved();
            });

        
        Utils.SigScan("Post-battle crossfade task", "40 53 48 83 EC 20 0F B7 D9 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? BA 02 00 00 00 B9 08 03 00 00 E8 ?? ?? ?? ?? 48 8D 0D", address =>
            {
                _createCrossfadeTask = Marshal.GetDelegateForFunctionPointer<TirednessPresentation.CreateCrossfadeTask>(address);
                PieceResolved();
            });

        
        Utils.SigScan("Battle sound task caller", "BA 02 00 00 00 8D 4A 08 44 8D 42 FF E8 ?? ?? ?? ?? B2 01 C6 00 05 48 8B 4F 58 48 89 48 08", address =>
            {
                _createSoundTask = Marshal.GetDelegateForFunctionPointer<TirednessPresentation.CreateSoundTask>(Utils.ResolveRelativeCall(address + 0x0C));
                PieceResolved();
            });
    }

    private void PieceResolved()
    {
        if (Interlocked.Increment(ref _resolvedPieces) == RequiredPieces)
            InstallHooks();
    }

    private void InstallHooks()
    {
        if (_installed)
            return;

        if (_fatigueAddress == 0 || _combatInfoAnchorAddress == 0 || _allDeadAddress == 0 ||
            _setUnitConditionAddress == 0 || _counterIncrementAddress == 0 ||
            _counterGetAddress == 0 || _bitSetThunkAddress == 0 || _randIntAddress == 0 ||
            _combatInfoGlobalAddress == 0 || _adjustEndurance is null || _getEndurance is null ||
            _getCondition is null || _setUnitCondition is null || _isCharacterDead is null ||
            _bitCheck is null || _randInt is null || _incrementBattleCounter is null ||
            _getBattleCounter is null || _createBattleStateTask is null ||
            _createCrossfadeTask is null || _createMotionTask is null || _createSoundTask is null ||
            _createActorVoiceTask is null || _createNavigatorTask is null || _enqueueBattleTask is null)
        {
            Utils.LogError("Native function resolution was incomplete; hooks were not installed.");
            return;
        }

        _presentation = new TirednessPresentation(
            _createBattleStateTask,
            _createCrossfadeTask,
            _createMotionTask,
            _createSoundTask,
            _createActorVoiceTask,
            _createNavigatorTask,
            _enqueueBattleTask);

        _fatigueHook = _hooks.CreateHook<FatigueCallback>(FatigueCallbackHook, _fatigueAddress).Activate();
        _fatigueOriginal = _fatigueHook.OriginalFunction;

        
        
        _victoryPresentationHook = _hooks.CreateHook<VictoryPresentationInit>(VictoryPresentationInitHook, _victoryPresentationAddress).Activate();
        _victoryPresentationOriginal = _victoryPresentationHook.OriginalFunction;

        
        nint bitSetTarget = Utils.ResolveRelativeJump(_bitSetThunkAddress);
        _bitSetHook = _hooks.CreateHook<BitSet>(BitSetHook, bitSetTarget).Activate();
        _bitSetOriginal = _bitSetHook.OriginalFunction;

        _installed = true;
        Utils.Log("FES tiredness restoration hooks installed. Live post-victory restoration uses the confirmed result initializer.");
        Debug($"Live post-victory initializer: P3P.exe+0x{(_victoryPresentationAddress - Utils.BaseAddress):X}.");
    }

    private ushort FatigueCallbackHook(nint unit)
    {
        ushort memberId = 0;
        nint characterInfo = 0;
        ushort enduranceBefore = 0;
        bool validPartyUnit = false;

        if (unit != 0)
        {
            var battleUnit = (BattleUnit*)unit;
            if (battleUnit->Type == 0 && battleUnit->MemberInfo != null)
            {
                memberId = battleUnit->MemberInfo->MemberId;
                characterInfo = (nint)battleUnit->MemberInfo;
                validPartyUnit = IsFatigueMember(memberId);

                if (validPartyUnit)
                    enduranceBefore = _getEndurance!(memberId);
            }
        }

        ushort result = _fatigueOriginal!(unit);

        if (!validPartyUnit)
            return result;

        ushort enduranceAfterNative = _getEndurance!(memberId);

        
        
        int drainMultiplier = GetFatigueDrainMultiplier();
        if (drainMultiplier > 1 && enduranceAfterNative < enduranceBefore)
        {
            _adjustEndurance!(memberId, checked((short)-(drainMultiplier - 1)));
        }

        ushort enduranceAfter = _getEndurance!(memberId);
        ushort condition = _getCondition!(memberId);
        Debug($"[ResultTrace] fatigue callback member={memberId}, return={result}, endurance={enduranceBefore}->{enduranceAfter}, condition={condition}, multiplier=x{drainMultiplier}.");

        
        if (condition <= 2 && enduranceAfter == 0)
        {
            _setUnitCondition!(characterInfo, Tired);
            ArmEntranceDeparture(memberId, Tired);
            Debug($"50% fatigue path: member {memberId} reached 0 endurance -> Tired.");
            return Tired;
        }

        return result;
    }

    private void VictoryPresentationInitHook(nint stateData)
    {

        

        nint combatInfo = GetCombatInfo();
        ulong taskBoundaryBeforeResult = TirednessPresentation.GetMaxQueuedTaskId(combatInfo);

        CombatModel* changedModel = null;
        BattleActor* changedActor = null;
        ushort changedMemberId = 0;
        ushort changedCondition = 0;

        bool changed = TryRunRestoredFesPostVictoryPass(
            out changedModel,
            out changedMemberId,
            out changedCondition);

        if (changed)
            changedActor = FindBattleActor(changedModel);

        

        
        _victoryPresentationOriginal!(stateData);
        ulong nativeResultTaskMax = TirednessPresentation.GetMaxQueuedTaskId(combatInfo);

        if (!changed)
            return;

        if (changedActor == null)
        {
            Utils.LogError($"Unable to find battle actor for member {changedMemberId}; condition changed but the FES presentation was skipped.");
            return;
        }

        if (!_presentation!.TryStart(
                changedActor,
                changedModel,
                changedCondition,
                out TirednessPresentation.PresentationHandle presentation))
        {
            Utils.LogError($"Unable to start the FES post-battle presentation for member {changedMemberId}.");
            return;
        }

        int gated = _presentation.GateTasksInRange(
            combatInfo,
            taskBoundaryBeforeResult,
            nativeResultTaskMax,
            presentation.CompletionTaskId,
            out int ungated);

        Debug($"Started FES after-battle presentation for member {changedMemberId}, condition {changedCondition}; " +
              $"result focus held by native task dependencies (gated={gated}, ungated={ungated}, completionTask={presentation.CompletionTaskId}).");

        if (ungated != 0)
        {
            Utils.LogError($"{ungated} native result task(s) had both dependency slots occupied; presentation ordering may be incomplete.");
        }
    }

    

    private bool TryRunRestoredFesPostVictoryPass(
        out CombatModel* changedModel,
        out ushort changedMemberId,
        out ushort changedCondition)
    {
        changedModel = null;
        changedMemberId = 0;
        changedCondition = 0;

        nint combatInfo = GetCombatInfo();
        if (combatInfo == 0)
            return false;

        
        _incrementBattleCounter!();

        if (_bitCheck!(SpecialOperationFlag) != 0)
        {
            Debug("Skipped restored FES post-victory pass because flag 0x172 is set.");
            return false;
        }

        int livingSickMembers = 0;

        
        
        int guard = 0;
        for (CombatModel* model = *(CombatModel**)(combatInfo + 0x1C0);
             model != null && guard++ < 64;
             model = model->Next)
        {
            if (!TryGetPartyCharacter(model, out nint characterInfo, out ushort memberId))
                continue;

            ushort condition = _getCondition!(memberId);

            if (condition <= 2)
            {
                ushort before = _getEndurance!(memberId);
                int drainMultiplier = GetFatigueDrainMultiplier();
                _adjustEndurance!(memberId, checked((short)-drainMultiplier));
                ushort after = _getEndurance!(memberId);
                Debug($"FES post-victory pass: member {memberId} endurance {before} -> {after} (x{drainMultiplier}).");
            }

            if (condition == Sick &&
                _isCharacterDead!(characterInfo, 0) == 0)
            {
                livingSickMembers++;
            }
        }

        bool contagionEvent = false;
        if (livingSickMembers > 0 && NextRandom(100) < 10)
        {

            
            _ = NextRandom((uint)livingSickMembers);
            contagionEvent = true;
        }

        

        bool anyChanged = false;
        int changedCount = 0;

        guard = 0;
        for (CombatModel* model = *(CombatModel**)(combatInfo + 0x1C0);
             model != null && guard++ < 64;
             model = model->Next)
        {
            if (!TryGetPartyCharacter(model, out nint characterInfo, out ushort memberId))
                continue;

            if (_isCharacterDead!(characterInfo, 0) != 0)
                continue;

            ushort oldCondition = _getCondition!(memberId);
            ushort newCondition = oldCondition;
            bool zeroEnduranceTransition = false;

            switch (oldCondition)
            {
                case 0:
                case 1:
                case 2:
                    if (_getEndurance!(memberId) == 0)
                    {
                        newCondition = Tired;
                        zeroEnduranceTransition = true;
                    }

                    
                    
                    if (!zeroEnduranceTransition && contagionEvent)
                    {
                        int sickChance = oldCondition switch
                        {
                            0 => 25,
                            1 => 10,
                            2 => 5,
                            _ => 0
                        };

                        if (sickChance != 0 && NextRandom(100) < sickChance && memberId != 3)
                        {
                            newCondition = Sick;
                        }
                    }
                    break;

                case Tired:
                    if (contagionEvent &&
                        NextRandom(100) < 60 &&
                        memberId != 3)
                    {
                        newCondition = Sick;
                    }
                    break;

                case 4:
                    if (memberId != 3)
                    {
                        uint chance = (uint)((_getBattleCounter!() & 0xFFFF) + 10);
                        if (NextRandom(100) < chance)
                        {
                            newCondition = Sick;
                        }
                    }
                    break;
            }

            if (newCondition == oldCondition)
                continue;

            _setUnitCondition!(characterInfo, newCondition);
            ArmEntranceDeparture(memberId, newCondition);
            anyChanged = true;
            changedCount++;

            if (changedModel == null)
            {
                changedModel = model;
                changedMemberId = memberId;
                changedCondition = newCondition;
            }

            Debug($"FES post-victory pass: member {memberId} condition {oldCondition} -> {newCondition}.");
        }

        if (changedCount > 1)
        {
            Debug($"FES post-victory pass: {changedCount} party members changed condition; member {changedMemberId} owns the single after-battle presentation.");
        }

        return anyChanged;
    }

    private void ArmEntranceDeparture(ushort memberId, ushort condition)
    {
        if (_bitSetOriginal is null || condition < Tired || !TryGetDepartureIndex(memberId, out int departureIndex))
            return;

        uint presenceFlag = FirstPresenceFlag + (uint)departureIndex;
        uint availabilityFlag = FirstAvailabilityFlag + (uint)departureIndex;
        _bitSetOriginal(presenceFlag, 0);
        _bitSetOriginal(availabilityFlag, 0);
        _bitSetOriginal(MemberReturnTriggerFlag, 1);
        Debug($"Entrance departure armed: member {memberId}, condition {condition}, presence {presenceFlag} OFF, availability {availabilityFlag} OFF, flag {MemberReturnTriggerFlag} ON.");
    }

    private static bool TryGetDepartureIndex(ushort memberId, out int departureIndex)
    {
        for (int i = 0; i < DepartureMembers.Length; i++)
        {
            if (DepartureMembers[i] == memberId)
            {
                departureIndex = i;
                return true;
            }
        }

        departureIndex = -1;
        return false;
    }

    private static bool IsFatigueMember(ushort memberId)
    {
        return memberId is 1 or 2 or 3 or 4 or 5 or 7 or 8 or 9 or 10;
    }

    private static bool IsDepartureMember(ushort memberId)
    {

        return memberId is 2 or 3 or 4 or 5 or 7 or 8 or 9 or 10;
    }

    private BattleActor* FindBattleActor(CombatModel* model)
    {
        nint combatInfo = GetCombatInfo();
        if (combatInfo == 0 || model == null)
            return null;

        int guard = 0;
        for (BattleActor* actor = *(BattleActor**)(combatInfo + 0x1B0);
             actor != null && guard++ < 64;
             actor = actor->Next)
        {
            if (actor->Model == model)
                return actor;
        }

        return null;
    }

    private nint GetCombatInfo()
    {
        if (_combatInfoGlobalAddress == 0)
            return 0;

        return *(nint*)_combatInfoGlobalAddress;
    }

    private void BitSetHook(uint flag, int state)
    {
        _bitSetOriginal!(flag, state);

        if (state != 0 ||
            flag < FirstPresenceFlag ||
            flag > LastPresenceFlag)
        {
            return;
        }

        int departureIndex = checked((int)(flag - FirstPresenceFlag));
        ushort memberId = DepartureMembers[departureIndex];
        ushort condition = _getCondition!(memberId);

        
        if (condition >= Tired)
        {
            uint availabilityFlag = FirstAvailabilityFlag + (uint)departureIndex;
            _bitSetOriginal(availabilityFlag, 0);
            Debug($"Departure sync: member {memberId}, presence {flag} OFF, availability {availabilityFlag} OFF.");
        }
    }

    private static bool TryGetPartyCharacter(CombatModel* model, out nint characterInfo, out ushort memberId)
    {
        characterInfo = 0;
        memberId = 0;

        if (model == null || model->MemberInfo == null)
            return false;

        characterInfo = (nint)model->MemberInfo;
        memberId = model->MemberInfo->MemberId;

        
        
        return IsFatigueMember(memberId);
    }

    private int GetFatigueDrainMultiplier()
    {
        int configured = _config.FatigueDrainMultiplier;
        int clamped = Math.Clamp(configured, 1, MaxFatigueDrainMultiplier);

        if (configured != clamped)
            Debug($"Fatigue Drain Multiplier {configured} is outside the supported range; using {clamped}.");

        return clamped;
    }

    private uint NextRandom(uint max)
    {
        if (max == 0)
            return 0;

        return _randInt!(max);
    }

    private void Debug(string message)
    {
        Utils.LogDebug(message);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort FatigueCallback(nint unit);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void VictoryPresentationInit(nint stateData);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void AdjustEndurance(ushort memberId, short amount);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort GetEndurance(ushort memberId);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate ushort GetCondition(ushort memberId);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void SetUnitCondition(nint characterInfo, ushort condition);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int IsCharacterDead(nint characterInfo, int unknown);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int BitCheck(uint flag);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate uint RandInt(uint max);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void IncrementBattleCounter();

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate uint GetBattleCounter();

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void BitSet(uint flag, int state);

}
