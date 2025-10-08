using UnityEngine;

public class FloatOnSingleAxis : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Floating Animation Settings")]
    public Axis floatAxis = Axis.Y;         // Dropdown menu in Inspector
    public float floatSpeed = 1.0f;         // Speed of the float motion
    public float floatAmplitude = 0.25f;    // Distance moved up/down

    private Vector3 startLocalPosition;

    void Start()
    {
        startLocalPosition = transform.localPosition;
    }

    void Update()
    {
        float offset = Mathf.Sin(Time.time * floatSpeed) * floatAmplitude;
        Vector3 animatedOffset = Vector3.zero;

        switch (floatAxis)
        {
            case Axis.X:
                animatedOffset = new Vector3(offset, 0, 0);
                break;
            case Axis.Y:
                animatedOffset = new Vector3(0, offset, 0);
                break;
            case Axis.Z:
                animatedOffset = new Vector3(0, 0, offset);
                break;
        }

        transform.localPosition = startLocalPosition + animatedOffset;
    }
}
