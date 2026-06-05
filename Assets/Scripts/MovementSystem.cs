using UnityEngine;

public class MovementSystem : MonoBehaviour
{
    private Rigidbody _rb;

    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float turnSpeed = 120f;

    public float MoveSpeed => moveSpeed;
    public float TurnSpeed => turnSpeed;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
            _rb = gameObject.AddComponent<Rigidbody>();
    }

    public void Move(Vector3 direction, float speed)
    {
        if (direction.sqrMagnitude < 0.01f) return;

        transform.position += direction.normalized * (speed * Time.deltaTime);
    }

    public void Rotate(Vector3 targetDirection)
    {
        if (targetDirection.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(targetDirection.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
    }

    public void RotateTowardTarget(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        Rotate(direction);
    }
}
