using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// About this issue:
//
// Animation Playable is affected by both Time.timeScale and Editor execution mode, which can result in inconsistent behavior.
// 
// I placed two characters with the same position in the scene and played the same animation through Playable.
// One character applied RootMotion using the `Animator.ApplyBuiltinRootMotion` method,
// and the other character applied RootMotion using data manually obtained from the `AnimationStream`.
// Normally, these two characters should always overlap after starting to play the animation,
// but in reality, their positions are affected by `Time.timeScale` and
// the execution mode of the Editor(AUTOMATIC or FRAME-BY-FRAME).
// 
// Specifically, the issue manifests as follows:
// a. When `Time.timeScale == 1`, the differences between the two animations start to become noticeable from the 3rd frame.
// b. When `Time.timeScale <= 0.85`, the two animations remain consistent.
// c. When `Time.timeScale >= 2`, the difference between the two animations is very small,
//    and the larger the value of Time.timeScale, the smaller the difference between the animations.
// d. When played FRAME BY FRAME, the two animations always remain consistent.
// e. When `Time.timeScale == 1`, playing the first 3 frames FRAME BY FRAME,
//    and then switching to automatic playback, the two animations can also always remain consistent.
// 
// In addition, `AnimationStream.velocity` and `AnimationStream.angularVelocity` are affected by `Time.timeScale`,
// but `AnimationStream.deltaTime` is not affected by `Time.timeScale`. This breaks the consistency of property states.
// 
// NOTE: For more information, please see the comment in "Sample.cs".

// How to reproduce:
// a. Open scene "Sample".
// b. Enter Play mode, then you will see two characters run to DIFFERENT positions.
// c. Exit Play mode.
// ----------
// d. Enter FRAME-BY-FRAME mode (the default shortcut key is Ctrl+Shift+P).
// e. Enter Play mode, play the game FRAME-BY-FRAME (the default shortcut key is Ctrl+Alt+P),
//    then you will see two characters run to the SAME position.
// f. Exit Play mode
// ----------
// g. Enter FRAME-BY-FRAME mode.
// h. Enter Play mode, playing the first 3 frames FRAME BY FRAME.
// i. Then switching to automatic playback, then you will see two characters run to the SAME position.
// j. Exit Play mode.
// ----------
// k. Exit FRAME-BY-FRAME mode.
// l. Set the Time Scale property to 0.5 in the "ProjectSettings-Time" panel.
// m. Enter Play mode, then you will see two characters run to the SAME position.

public struct AnimGraphRootJob : IAnimationJob
{
    public NativeReference<Vector3> velocityRef;

    public NativeReference<Vector3> angularVelocityRef;

    public NativeReference<float> deltaTimeRef;

    public void ProcessRootMotion(AnimationStream stream)
    {
        velocityRef.Value = stream.velocity;
        angularVelocityRef.Value = stream.angularVelocity;
        deltaTimeRef.Value = stream.deltaTime;
    }

    public void ProcessAnimation(AnimationStream stream) { }
}

[RequireComponent(typeof(Animator))]
public class Sample : MonoBehaviour
{
    public AnimationClip clip;
    public bool usePlayableRootMotion = true;
    // When Time.deltaTime equals 1, the differences between the two animations start to become noticeable from the 3rd frame.
    public ulong pauseOnFrame = 3;

    private PlayableGraph _graph;
    private Animator _animator;
    private NativeReference<Vector3> _velocityRef;
    private NativeReference<Vector3> _angularVelocityRef;
    private NativeReference<float> _deltaTimeRef;
    private ulong _animFrameId;


    private void Awake()
    {
        Application.targetFrameRate = 30;

        _graph = PlayableGraph.Create($"PlayableGraph@{name}");
        _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        _animator = GetComponent<Animator>();

        _velocityRef = new NativeReference<Vector3>(Allocator.Persistent);
        _angularVelocityRef = new NativeReference<Vector3>(Allocator.Persistent);
        _deltaTimeRef = new NativeReference<float>(Allocator.Persistent);

        // playables
        var majorAcp = AnimationClipPlayable.Create(_graph, clip);
        var rootAsp = AnimationScriptPlayable.Create(_graph, new AnimGraphRootJob
        {
            velocityRef = _velocityRef,
            angularVelocityRef = _angularVelocityRef,
            deltaTimeRef = _deltaTimeRef,
        });
        rootAsp.AddInput(majorAcp, 0, 1f);

        // output
        var apo = AnimationPlayableOutput.Create(_graph, "Anim", _animator);
        apo.SetSourcePlayable(rootAsp);

        _graph.Play();
    }

    private void OnAnimatorMove()
    {
        _animFrameId++;

        float deltaTime;
        bool hasVelocity;
        if (usePlayableRootMotion)
        {
            // In this mode, except for the first few frames, deltaTime is always equal to Time.unscaledDeltaTime.
            // This indicates that AnimationStream.velocity and AnimationStream.angularVelocity are affected by Time.deltaTime,
            // but AnimationStream.deltaTime is not affected by Time.deltaTime.
            // 
            // Properties of the same type having inconsistent states is a poor design,
            // and this point is not explained in the documentation.
            // 
            // The deltaTime property of the FrameData parameter in the PlayableBehaviour.PrepareFrame method also has this issue.
            deltaTime = _deltaTimeRef.Value;
            hasVelocity = _velocityRef.Value.sqrMagnitude > 0.001f;
            ApplyComponentSpaceVelocity(_animator, _velocityRef.Value, _angularVelocityRef.Value, _deltaTimeRef.Value);
        }
        else
        {
            deltaTime = Time.deltaTime;
            hasVelocity = _animator.velocity.sqrMagnitude > 0.001f;
            _animator.ApplyBuiltinRootMotion();
        }

        string rootMotionMode = usePlayableRootMotion ? "Playable Root Motion" : "Builtin Root Motion";
        if (hasVelocity) Debug.Log($"#{_animFrameId}  dt:{deltaTime:F3}  {rootMotionMode}");

        // When Time.deltaTime == 1, the animation starts to show noticeable differences from the 3rd frame.
        if (_animFrameId == pauseOnFrame) Debug.Break();
    }

    private void OnDestroy()
    {
        if (_graph.IsValid()) _graph.Destroy();
        if (_velocityRef.IsCreated) _velocityRef.Dispose();
        if (_angularVelocityRef.IsCreated) _angularVelocityRef.Dispose();
        if (_deltaTimeRef.IsCreated) _deltaTimeRef.Dispose();
    }


    public static void ApplyComponentSpaceVelocity(Animator target,
        Vector3 compSpaceVelocity, Vector3 compSpaceAngularVelocityInRadian, float deltaTime)
    {
        var targetTransform = target.transform;
        var compSpaceDeltaRotation = Quaternion.Euler(compSpaceAngularVelocityInRadian * Mathf.Rad2Deg * deltaTime);
        var worldSpaceDeltaPosition = targetTransform.TransformDirection(compSpaceVelocity) * deltaTime;
        targetTransform.rotation = compSpaceDeltaRotation * targetTransform.rotation;
        targetTransform.position += worldSpaceDeltaPosition;
    }
}
