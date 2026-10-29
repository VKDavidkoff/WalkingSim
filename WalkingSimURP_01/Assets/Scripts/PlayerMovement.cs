using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Keyboard control (turning is always keyboard; forward is keyboard only as a fallback)")]
    public float moveSpeed = 4.0f;
    public float turnSpeedDegPerSec = 120f;
    public float gravity = -9.81f;

    [Header("Treadmill input")]
    [Tooltip("When on, forward speed comes from TreadmillClient (if connected and receiving fresh data); otherwise falls back to W/S.")]
    public bool useTreadmillInput = true;
    [Tooltip("Scales the treadmill's real-world speed before it's applied in-game. 1.0 = literal real-world speed (often feels slow); raise this to make in-game movement feel faster than actual walking/running pace. Tunable live in Play mode.")]
    public float treadmillSpeedMultiplier = 2.0f;

    private CharacterController controller;
    private float verticalVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Turning is always keyboard-driven -- the treadmill only reports
        // forward speed, not direction.
        float turn = Input.GetAxis("Horizontal") * turnSpeedDegPerSec * Time.deltaTime;
        transform.Rotate(0f, turn, 0f);

        bool treadmillActive = useTreadmillInput && TreadmillClient.Instance != null && TreadmillClient.Instance.IsDataFresh;

        float forwardSpeed;
        if (treadmillActive)
        {
            forwardSpeed = TreadmillClient.Instance.SpeedMetersPerSecond * treadmillSpeedMultiplier;
        }
        else
        {
            float forwardInput = Input.GetAxis("Vertical");
            forwardSpeed = forwardInput * moveSpeed;
        }

        Vector3 move = transform.forward * forwardSpeed;

        if (controller.isGrounded)
        {
            verticalVelocity = -0.5f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);
    }
}
