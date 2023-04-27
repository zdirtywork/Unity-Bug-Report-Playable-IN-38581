# [Won't Fix] Unity-Bug-Report-Playable-IN-38581

**Unity has stated that they will not fix this bug.**

## About this issue

Animation Playable is affected by both Time.timeScale and Editor execution mode, which can result in inconsistent behavior.

I placed two characters with the same position in the scene and played the same animation through Playable.
One character applied RootMotion using the `Animator.ApplyBuiltinRootMotion` method,
and the other character applied RootMotion using data manually obtained from the `AnimationStream`.
Normally, these two characters should always overlap after starting to play the animation,
but in reality, their positions are affected by `Time.timeScale` and
the execution mode of the Editor(AUTOMATIC or FRAME-BY-FRAME).

Specifically, the issue manifests as follows:
a. When `Time.deltaTime == 1`, the differences between the two animations start to become noticeable from the 3rd frame.
b. When `Time.deltaTime >= 0.85`, the two animations remain consistent.
c. When `Time.deltaTime >= 2`, the difference between the two animations is very small,
   and the larger the value of Time.deltaTime, the smaller the difference between the animations.
d. When played FRAME BY FRAME, the two animations always remain consistent.
e. When `Time.deltaTime == 1`, playing the first 3 frames FRAME BY FRAME,
   and then switching to automatic playback, the two animations can also always remain consistent.

In addition, `AnimationStream.velocity` and `AnimationStream.angularVelocity` are affected by `Time.timeScale`,
but `AnimationStream.deltaTime` is not affected by `Time.timeScale`. This breaks the consistency of property states.

> NOTE: For more information, please see the comment in "Sample.cs".

## How to reproduce

Situation 1:

1. Open scene "Sample".
2. Enter Play mode, then you will see two characters run to DIFFERENT positions.
3. Exit Play mode.

Situation 2:

1. Enter FRAME-BY-FRAME mode (the default shortcut key is Ctrl+Shift+P).
2. Enter Play mode, play the game FRAME-BY-FRAME (the default shortcut key is Ctrl+Alt+P), then you will see two characters run to the SAME position.
3. Exit Play mode

Situation 3:

1. Enter FRAME-BY-FRAME mode.
2. Enter Play mode, playing the first 3 frames FRAME BY FRAME.
3. Then switching to automatic playback, then you will see two characters run to the SAME position.
4. Exit Play mode.

Situation 4:

1. Exit FRAME-BY-FRAME mode.
2. Set the Time Scale property to 0.5 in the "ProjectSettings-Time" panel.
3. Enter Play mode, then you will see two characters run to the SAME position.
