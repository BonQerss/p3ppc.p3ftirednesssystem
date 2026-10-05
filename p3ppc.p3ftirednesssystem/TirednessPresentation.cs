using System.Runtime.InteropServices;
using static p3ppc.p3ftirednesssystem.NativeTypes;

namespace p3ppc.p3ftirednesssystem;

internal unsafe sealed class TirednessPresentation
{
    private const ushort Tired = 3;
    private const ushort Sick = 5;

    private const ushort FesTiredActorState = 0x21;
    private const uint PortableResultCameraTaskType = 0x60A;

    private const byte WaitForCompletion = 4;
    private const byte WaitForActivePhase = 5;
    private const byte NoDependency = 1;

    private readonly CreateBattleStateTask _createBattleStateTask;
    private readonly CreateCrossfadeTask _createCrossfadeTask;
    private readonly CreateMotionTask _createMotionTask;
    private readonly CreateSoundTask _createSoundTask;
    private readonly CreateActorVoiceTask _createActorVoiceTask;
    private readonly CreateNavigatorTask _createNavigatorTask;
    private readonly EnqueueBattleTask _enqueueBattleTask;

    internal readonly record struct PresentationHandle(ulong CompletionTaskId);

    internal TirednessPresentation(CreateBattleStateTask createBattleStateTask, CreateCrossfadeTask createCrossfadeTask, CreateMotionTask createMotionTask, CreateSoundTask createSoundTask, CreateActorVoiceTask createActorVoiceTask, CreateNavigatorTask createNavigatorTask, EnqueueBattleTask enqueueBattleTask)
    {
        _createBattleStateTask = createBattleStateTask;
        _createCrossfadeTask = createCrossfadeTask;
        _createMotionTask = createMotionTask;
        _createSoundTask = createSoundTask;
        _createActorVoiceTask = createActorVoiceTask;
        _createNavigatorTask = createNavigatorTask;
        _enqueueBattleTask = enqueueBattleTask;
    }

    internal bool TryStart(BattleActor* actor, CombatModel* model, ushort condition, out PresentationHandle handle)
    {
        handle = default;

        if (actor == null || model == null)
        {
            return false;
        }

        ushort navigatorEvent;
        ushort actorVoiceEvent;

        switch (condition)
        {
            case Tired:
                navigatorEvent = 0x12;
                actorVoiceEvent = 0x13;
                break;

            case 4:
            case Sick:
                navigatorEvent = 0x10;
                actorVoiceEvent = 0x12;
                break;

            default:
                return false;
        }

        var baseTask = (BattleTask*)_createBattleStateTask((nint)actor, FesTiredActorState);
        if (baseTask == null)
        {
            return false;
        }

        baseTask->OwnerToken = actor->OwnerToken;
        baseTask->Delay = 8.0f;
        _enqueueBattleTask((nint)baseTask, 0);
        ulong baseTaskId = baseTask->TaskId;

        var crossfadeTask = (BattleTask*)_createCrossfadeTask(8);
        if (crossfadeTask != null)
        {
            SetPreDependency(crossfadeTask, WaitForActivePhase, baseTaskId);
            _enqueueBattleTask((nint)crossfadeTask, 1);
        }

        var motionTask = (BattleTask*)_createMotionTask((nint)model, 3, 0, 1.0f, 1);
        if (motionTask == null)
        {
            return false;
        }

        SetPreDependency(motionTask, WaitForActivePhase, baseTaskId);
        _enqueueBattleTask((nint)motionTask, 1);

        var soundTask = (BattleTask*)_createSoundTask(0xE, 2, 9);
        if (soundTask == null)
        {
            return false;
        }

        SetPreDependency(soundTask, WaitForActivePhase, baseTaskId);
        _enqueueBattleTask((nint)soundTask, 1);

        SetCompletionDependency(soundTask, 0, WaitForCompletion, baseTaskId);
        if (crossfadeTask != null)
        {
            SetCompletionDependency(soundTask, 1, WaitForCompletion, crossfadeTask->TaskId);
        }

        var actorVoiceTask = (BattleTask*)_createActorVoiceTask((nint)actor, actorVoiceEvent, 0, 0, 0);
        if (actorVoiceTask == null)
        {
            return false;
        }

        SetPreDependency(actorVoiceTask, WaitForActivePhase, baseTaskId);
        _enqueueBattleTask((nint)actorVoiceTask, 1);

        var navigatorTask = (BattleTask*)_createNavigatorTask((nint)actor, navigatorEvent, 0, 0, 1);
        if (navigatorTask == null)
        {
            return false;
        }

        SetPreDependency(navigatorTask, WaitForCompletion, actorVoiceTask->TaskId);

        SetCompletionDependency(navigatorTask, 0, WaitForCompletion, motionTask->TaskId);
        SetCompletionDependency(navigatorTask, 1, WaitForCompletion, soundTask->TaskId);
        _enqueueBattleTask((nint)navigatorTask, 1);

        handle = new PresentationHandle(navigatorTask->TaskId);
        return true;
    }

    internal static ulong RetargetResultCameraInRange(nint combatInfo, ulong firstTaskIdExclusive, ulong lastTaskIdInclusive, CombatModel* model)
    {
        if (combatInfo == 0 || model == null || lastTaskIdInclusive <= firstTaskIdExclusive)
        {
            return 0;
        }

        const int resultCameraQueue = 1;
        int guard = 0;
        for (BattleTask* task = *(BattleTask**)(combatInfo + 0x200 + resultCameraQueue * 0x10); task != null && guard++ < 512; task = task->Next)
        {
            if (task->TaskId <= firstTaskIdExclusive || task->TaskId > lastTaskIdInclusive)
            {
                continue;
            }

            if (task->TaskType != PortableResultCameraTaskType || task->Args == null)
            {
                continue;
            }

            float x = model->RotationX;
            float y = model->RotationY;
            float z = model->RotationZ;
            float w = model->RotationW;

            if (!IsFiniteQuaternion(x, y, z, w))
            {
                return 0;
            }

            float* cameraQuaternion = (float*)task->Args;
            cameraQuaternion[0] = z;
            cameraQuaternion[1] = w;
            cameraQuaternion[2] = -x;
            cameraQuaternion[3] = -y;

            task->Delay = 8.0f;
            return task->TaskId;
        }

        return 0;
    }

    private static bool IsFiniteQuaternion(float x, float y, float z, float w)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z) || !float.IsFinite(w))
        {
            return false;
        }

        float lengthSquared = x * x + y * y + z * z + w * w;
        return float.IsFinite(lengthSquared) && lengthSquared > 0.000001f;
    }

    internal static ulong GetMaxQueuedTaskId(nint combatInfo)
    {
        if (combatInfo == 0)
        {
            return 0;
        }

        ulong max = 0;
        for (int queue = 0; queue < 4; queue++)
        {
            int guard = 0;
            for (BattleTask* task = *(BattleTask**)(combatInfo + 0x200 + queue * 0x10); task != null && guard++ < 512; task = task->Next)
            {
                if (task->TaskId > max)
                {
                    max = task->TaskId;
                }
            }
        }

        return max;
    }

    internal int GateTasksInRange(nint combatInfo, ulong firstTaskIdExclusive, ulong lastTaskIdInclusive, ulong completionTaskId, ulong excludedTaskId, out int ungatedTasks)
    {
        ungatedTasks = 0;
        if (combatInfo == 0 || completionTaskId == 0 || lastTaskIdInclusive <= firstTaskIdExclusive)
        {
            return 0;
        }

        int gated = 0;

        for (int queue = 0; queue < 4; queue++)
        {
            int guard = 0;
            for (BattleTask* task = *(BattleTask**)(combatInfo + 0x200 + queue * 0x10); task != null && guard++ < 512; task = task->Next)
            {
                if (task->TaskId <= firstTaskIdExclusive || task->TaskId > lastTaskIdInclusive)
                {
                    continue;
                }

                if (excludedTaskId != 0 && task->TaskId == excludedTaskId)
                {
                    continue;
                }

                if (task->DependencyType2 == NoDependency)
                {
                    task->DependencyType2 = WaitForCompletion;
                    task->DependencyTaskId2 = completionTaskId;
                    gated++;
                    continue;
                }

                if (task->DependencyType == NoDependency)
                {
                    task->DependencyType = WaitForCompletion;
                    task->DependencyTaskId = completionTaskId;
                    gated++;
                    continue;
                }

                ungatedTasks++;
            }
        }

        return gated;
    }

    private static void SetPreDependency(BattleTask* task, byte type, ulong taskId)
    {
        task->DependencyType = type;
        task->DependencyTaskId = taskId;
    }

    private static void SetCompletionDependency(BattleTask* task, int slot, byte type, ulong taskId)
    {
        if (slot == 0)
        {
            task->CompletionDependencyType = type;
            task->CompletionDependencyTaskId = taskId;
        }
        else
        {
            task->CompletionDependencyType2 = type;
            task->CompletionDependencyTaskId2 = taskId;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate nint CreateBattleStateTask(nint actor, ushort state);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate nint CreateCrossfadeTask(ushort command);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate nint CreateMotionTask(nint model, ushort motionId, int unknown, float speed, ushort mode);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate nint CreateSoundTask(ushort command, ushort mode, ushort soundId);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate nint CreateActorVoiceTask(nint actor, ushort eventId, int arg2, nint arg3, ushort arg4);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate nint CreateNavigatorTask(nint actor, ushort eventId, int arg2, int arg3, uint arg4);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate ulong EnqueueBattleTask(nint task, byte queue);
}
