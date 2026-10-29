using UnityEngine;

public class SimpleFollowCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 3f, -5f);
    public float smoothTime = 0.15f;

    private Vector3 velocity;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + target.TransformDirection(offset);
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime);
        transform.LookAt(target.position + Vector3.up * 1.5f);
    }
}
