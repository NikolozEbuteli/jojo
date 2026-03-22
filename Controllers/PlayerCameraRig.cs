using UnityEngine;

public class PlayerCameraRig : MonoBehaviour
{
    [SerializeField] private Camera cam;

    [Header("Follow")]
    [SerializeField] private float followSpeed = 10f;
    [SerializeField] private float deadzone = 0.75f;
    [SerializeField] private float maxSurfOffset = 2.0f;
    [SerializeField] private float offsetSmooth = 12f;

    [Header("Lock On")]
    [SerializeField] private float lockRange = 8f;
    [SerializeField] private KeyCode lockKey = KeyCode.L;

    private Transform target;
    private Transform lockedTarget;
    private bool lockOn;

    private Vector2 currentOffset;

    private void Awake()
    {
        if (cam == null)
            cam = GetComponentInChildren<Camera>(true);
    }

    public void SetTarget(Transform playerTarget)
    {
        target = playerTarget;

        if (cam != null && target != null)
        {
            cam.transform.position = new Vector3(target.position.x, target.position.y, -10f);
            cam.transform.rotation = Quaternion.identity;
        }
    }

    public Vector2 GetAimDirectionFromPlayer(Vector3 playerPosition)
    {
        if (cam == null)
            return Vector2.up;

        if (lockOn && lockedTarget != null)
        {
            Vector2 dir = (Vector2)lockedTarget.position - (Vector2)playerPosition;
            return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
        }

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        Vector2 dirMouse = (Vector2)mouseWorld - (Vector2)playerPosition;
        return dirMouse.sqrMagnitude > 0.0001f ? dirMouse.normalized : Vector2.up;
    }

    private void Update()
    {
        if (target == null || cam == null) return;

        if (Input.GetKeyDown(lockKey) || Input.GetMouseButtonDown(2))
            ToggleLockOn();

        if (lockOn && !TargetStillValid())
        {
            lockOn = false;
            lockedTarget = null;
        }
    }

    private void LateUpdate()
    {
        if (target == null || cam == null) return;

        Vector2 targetOffset = ComputeCameraOffset();

        currentOffset = Vector2.Lerp(
            currentOffset,
            targetOffset,
            offsetSmooth * Time.deltaTime
        );

        Vector3 desiredPos = target.position + new Vector3(currentOffset.x, currentOffset.y, -10f);

        cam.transform.position = Vector3.Lerp(
            cam.transform.position,
            desiredPos,
            followSpeed * Time.deltaTime
        );

        cam.transform.rotation = Quaternion.identity;
    }

    private Vector2 ComputeCameraOffset()
    {
        Vector2 aimDir = GetAimDirectionFromPlayer(target.position);

        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        float dist = Vector2.Distance(target.position, mouseWorld);

        if (dist <= deadzone)
            return Vector2.zero;

        float t = Mathf.InverseLerp(deadzone, deadzone + maxSurfOffset, dist);
        float offsetAmount = Mathf.Lerp(0f, maxSurfOffset, t);

        return aimDir * offsetAmount;
    }

    private void ToggleLockOn()
    {
        if (lockOn)
        {
            lockOn = false;
            lockedTarget = null;
            return;
        }

        lockedTarget = FindClosestPlayerInRange();
        lockOn = lockedTarget != null;
    }

    private Transform FindClosestPlayerInRange()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        Transform best = null;
        float bestDistSq = lockRange * lockRange;

        foreach (GameObject go in players)
        {
            if (go == target.gameObject) continue;

            float distSq = ((Vector2)go.transform.position - (Vector2)target.position).sqrMagnitude;
            if (distSq <= bestDistSq)
            {
                bestDistSq = distSq;
                best = go.transform;
            }
        }

        return best;
    }

    private bool TargetStillValid()
    {
        if (lockedTarget == null) return false;

        float distSq = ((Vector2)lockedTarget.position - (Vector2)target.position).sqrMagnitude;
        return distSq <= lockRange * lockRange;
    }
}