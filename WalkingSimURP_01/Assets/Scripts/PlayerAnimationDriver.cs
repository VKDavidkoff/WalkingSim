using UnityEngine;

// This is where the Animator's float comes from. The Blend Tree inside
// WalkingSim_Anim only *reads* a number called "Speed"; nothing in Unity fills
// it in by itself, so this script writes it every frame.
//
// The number is how fast the player is actually moving across the ground (m/s,
// in game units), read from the CharacterController. That makes it work for any
// input source -- treadmill or the keyboard fallback -- without this script
// needing to know about either.
[RequireComponent(typeof(CharacterController))]
public class PlayerAnimationDriver : MonoBehaviour
{
    [Tooltip("The model's Animator. Found automatically in the children if left empty.")]
    public Animator animator;

    [Tooltip("Must match the float parameter's name in the Animator Controller.")]
    public string speedParameter = "Speed";

    [Tooltip("Smoothing in seconds. The treadmill reports about 4 times a second, so without this the animation would jump between speeds.")]
    public float dampTime = 0.15f;

    private CharacterController controller;
    private int speedHash;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        speedHash = Animator.StringToHash(speedParameter);
    }

    void Update()
    {
        if (animator == null) return;

        Vector3 groundVelocity = controller.velocity;
        groundVelocity.y = 0f; // falling or walking up a slope shouldn't count as running faster

        animator.SetFloat(speedHash, groundVelocity.magnitude, dampTime, Time.deltaTime);
    }
}
