using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DepthGravity : MonoBehaviour
{
    [SerializeField] private float gravityStrength = 20f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        rb.AddForce(Vector3.forward * gravityStrength, ForceMode.Acceleration);
    }
}