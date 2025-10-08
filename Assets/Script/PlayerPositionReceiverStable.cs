using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using OscSimpl;

public class PlayerPositionReceiverStable : MonoBehaviour
{
    [Header("OSC")]
    public OscIn oscIn;
    public int openPort = 12000;
    public string addrCount = "/Count";
    public string addrPosition = "/Position";

    [Header("좌표 매핑")]
    public float minX = -5f, maxX = 5f, minZ = -5f, maxZ = 5f;

    [Header("스무딩/타임아웃")]
    public float smoothTime = 0.08f;
    public float timeoutSec = 0.5f;

    [Header("최대 인원")]
    public int maxPlayers = 20;

    [Header("트래킹 매개변수")]
    public float assignMaxDistance = 1.2f;
    public float spawnCooldown = 0.15f;

    [Header("프리팹")]
    public GameObject playerPrefab;
    public GameObject playerVfxPrefab;
    public GameObject clusterVfxPrefab; // 군집 VFX 프리팹(동시 다수)

    [Header("군집 조건")]
    public int clusterMinMembers = 3;
    public float clusterRadius = 1.5f; // m
    public int maxClusters = 8;

    [Header("VFX 키")]
    public string vfxParam_PlayerPos = "PlayerPos";
    public string vfxEvent_OnAppear = "OnAppear";
    public string vfxEvent_OnUpdate = "RipplePulse";
    public string vfxParam_ClusterCenter = "ClusterCenter";
    public string vfxEvent_ClusterBurst = "ClusterBurst";
    public string vfxEvent_ClusterSustain = "ClusterSustain";

    // -------- 내부 --------
    class Tracked
    {
        public Transform tracker;
        public Transform vfxTf;
        public VisualEffect vfx;
        public Vector3 vel;
        public float lastSeen;
        public float lastSpawnT;
        public bool active;
        public bool matchedThisFrame;
        public Vector3 matchedPos;
    }
    class Pool
    {
        readonly GameObject prefab; readonly Transform parent; readonly Stack<GameObject> s = new();
        public Pool(GameObject p, Transform t, int preload = 0)
        {
            prefab = p; parent = t;
            for (int i = 0; i < preload; i++) { var go = GameObject.Instantiate(prefab, parent); go.SetActive(false); s.Push(go); }
        }
        public GameObject Get() { var go = s.Count > 0 ? s.Pop() : GameObject.Instantiate(prefab, parent); go.SetActive(true); return go; }
        public void Release(GameObject go) { go.SetActive(false); go.transform.SetParent(parent, false); s.Push(go); }
    }

    Tracked[] slots;
    Pool poolTracker, poolVfx, poolCluster;
    readonly List<VisualEffect> clusterFx = new();
    readonly List<Transform> clusterTf = new();

    void Awake()
    {
        if (!playerPrefab || !playerVfxPrefab) { Debug.LogError("prefab missing"); enabled = false; return; }
        poolTracker = new Pool(playerPrefab, transform, maxPlayers);
        poolVfx = new Pool(playerVfxPrefab, transform, maxPlayers);
        if (clusterVfxPrefab) poolCluster = new Pool(clusterVfxPrefab, transform, Mathf.Max(1, maxClusters));

        slots = new Tracked[maxPlayers];
        for (int i = 0; i < maxPlayers; i++)
        {
            var tGo = poolTracker.Get(); var vGo = poolVfx.Get();
            var tr = new Tracked
            {
                tracker = tGo.transform,
                vfxTf = vGo.transform,
                vfx = vGo.GetComponent<VisualEffect>(),
                vel = Vector3.zero,
                lastSeen = -999f,
                lastSpawnT = -999f,
                active = false
            };
            tGo.transform.position = Vector3.zero; vGo.transform.position = Vector3.zero;
            SetActive(tr, false);
            slots[i] = tr;
        }
    }

    void Start()
    {
        if (!oscIn) { Debug.LogError("OscIn missing"); enabled = false; return; }
        if (openPort > 0 && !oscIn.isOpen) oscIn.Open(openPort);
        oscIn.filterDuplicates = true;
        oscIn.MapInt(addrCount, OnCount);
        oscIn.Map(addrPosition, OnPosition);
    }

    void OnDestroy()
    {
        if (!oscIn) return;
        oscIn.UnmapInt(OnCount);
        oscIn.Unmap(OnPosition);
    }

    int countHint = 0;
    void OnCount(int c) { countHint = Mathf.Clamp(c, 0, maxPlayers); } // 참고값만

    void OnPosition(OscMessage m)
    {
        int n = Mathf.Min(m.Count() / 2, maxPlayers);
        var det = new List<Vector3>(n);
        for (int i = 0; i < n; i++)
        {
            if (!m.TryGet(i * 2, out float sx) || !m.TryGet(i * 2 + 1, out float sy)) continue;
            float x = Mathf.Lerp(minX, maxX, Mathf.Clamp01(sx));
            float z = Mathf.Lerp(minZ, maxZ, Mathf.Clamp01(sy));
            det.Add(new Vector3(x, 0f, z));
        }

        for (int i = 0; i < maxPlayers; i++) slots[i].matchedThisFrame = false;

        var pairs = new List<(int track, int det, float d2)>();
        float assignR2 = assignMaxDistance * assignMaxDistance;
        for (int i = 0; i < maxPlayers; i++)
        {
            if (!slots[i].active) continue;
            Vector3 p = slots[i].tracker.position;
            for (int j = 0; j < det.Count; j++)
            {
                float d2 = (p - det[j]).sqrMagnitude;
                if (d2 <= assignR2) pairs.Add((i, j, d2));
            }
        }
        pairs.Sort((a, b) => a.d2.CompareTo(b.d2));

        var detUsed = new bool[det.Count];
        foreach (var p in pairs)
        {
            if (slots[p.track].matchedThisFrame) continue;
            if (detUsed[p.det]) continue;
            slots[p.track].matchedThisFrame = true;
            slots[p.track].matchedPos = det[p.det];
            detUsed[p.det] = true;
        }

        for (int j = 0; j < det.Count; j++)
        {
            if (detUsed[j]) continue;
            int free = FindFreeSlot();
            if (free >= 0)
            {
                var tr = slots[free];
                if (Time.time - tr.lastSpawnT < spawnCooldown) continue;
                WarpAndActivate(tr, det[j], fireAppear: false);
                tr.matchedThisFrame = true;
                tr.matchedPos = det[j];
            }
        }

        for (int i = 0; i < maxPlayers; i++)
        {
            var tr = slots[i];
            if (!tr.active) continue;

            if (tr.matchedThisFrame)
            {
                Vector3 cur = tr.tracker.position;
                Vector3 tgt = tr.matchedPos;
                Vector3 next = Vector3.SmoothDamp(cur, tgt, ref tr.vel, smoothTime);
                next.x = Mathf.Clamp(next.x, minX, maxX); next.z = Mathf.Clamp(next.z, minZ, maxZ);
                tr.tracker.position = next; tr.vfxTf.position = next;
                tr.lastSeen = Time.time;

                if (tr.vfx)
                {
                    tr.vfx.SetVector3(vfxParam_PlayerPos, next);
                    if (!string.IsNullOrEmpty(vfxEvent_OnUpdate)) tr.vfx.SendEvent(vfxEvent_OnUpdate);
                }
            }
        }

        OscPool.Recycle(m);
    }

    void Update()
    {
        float now = Time.time;
        for (int i = 0; i < maxPlayers; i++)
        {
            var tr = slots[i];
            if (tr.active && now - tr.lastSeen > timeoutSec) SetActive(tr, false);
        }

        HandleClusters(); // 군집 이펙트
    }

    // -------- 군집 --------
    struct Cluster { public Vector3 center; public int size; }

    void HandleClusters()
    {
        var pts = new List<Vector3>();
        for (int i = 0; i < maxPlayers; i++) if (slots[i].active) pts.Add(slots[i].tracker.position);

        var clusters = FindClusters(pts, clusterRadius, clusterMinMembers, maxClusters);
        SyncClusterFxCount(clusters.Count);

        for (int i = 0; i < clusters.Count; i++)
        {
            var c = clusters[i];
            var tf = clusterTf[i];
            var fx = clusterFx[i];

            tf.position = c.center;
            if (fx)
            {
                fx.SetVector3(vfxParam_ClusterCenter, c.center);
                if (!string.IsNullOrEmpty(vfxEvent_ClusterSustain)) fx.SendEvent(vfxEvent_ClusterSustain);
            }
        }
    }

    static List<Cluster> FindClusters(List<Vector3> pts, float radius, int minMembers, int maxClusters)
    {
        var clusters = new List<Cluster>();
        int n = pts.Count; if (n == 0 || minMembers <= 1) return clusters;
        bool[] used = new bool[n];
        float r2 = radius * radius;

        for (int i = 0; i < n; i++)
        {
            if (used[i]) continue;
            var mem = new List<int> { i };
            for (int j = 0; j < n; j++)
            {
                if (i == j || used[j]) continue;
                if ((pts[j] - pts[i]).sqrMagnitude <= r2) mem.Add(j);
            }
            if (mem.Count >= minMembers)
            {
                Vector3 sum = Vector3.zero; foreach (int k in mem) sum += pts[k];
                Vector3 center = sum / mem.Count;

                // refine 1회
                var mem2 = new List<int>();
                for (int j = 0; j < n; j++) if ((pts[j] - center).sqrMagnitude <= r2) mem2.Add(j);
                if (mem2.Count >= minMembers)
                {
                    sum = Vector3.zero; foreach (int k in mem2) { sum += pts[k]; used[k] = true; }
                    center = sum / mem2.Count;
                    clusters.Add(new Cluster { center = center, size = mem2.Count });
                    if (clusters.Count >= maxClusters) break;
                }
            }
            else used[i] = true;
        }
        return clusters;
    }

    void SyncClusterFxCount(int needed)
    {
        // 늘리기
        while (clusterFx.Count < needed)
        {
            GameObject go = poolCluster != null ? poolCluster.Get() : Instantiate(clusterVfxPrefab, transform);
            var fx = go.GetComponent<VisualEffect>(); var tf = go.transform;
            clusterFx.Add(fx); clusterTf.Add(tf);
            if (fx && !string.IsNullOrEmpty(vfxEvent_ClusterBurst)) fx.SendEvent(vfxEvent_ClusterBurst);
        }
        // 줄이기
        for (int i = clusterFx.Count - 1; i >= needed; i--)
        {
            var go = clusterFx[i].gameObject;
            clusterFx.RemoveAt(i); clusterTf.RemoveAt(i);
            if (poolCluster != null) poolCluster.Release(go); else Destroy(go);
        }
    }

    // -------- 유틸 --------
    int FindFreeSlot()
    {
        for (int i = 0; i < maxPlayers; i++) if (!slots[i].active) return i;
        int oldest = -1; float oldestAge = -1f;
        for (int i = 0; i < maxPlayers; i++) { float age = Time.time - slots[i].lastSeen; if (age > oldestAge) { oldestAge = age; oldest = i; } }
        if (oldest >= 0) SetActive(slots[oldest], false);
        return oldest;
    }

    void WarpAndActivate(Tracked tr, Vector3 pos, bool fireAppear)
    {
        tr.tracker.position = pos; tr.vfxTf.position = pos;
        tr.vel = Vector3.zero; tr.lastSeen = Time.time; tr.lastSpawnT = Time.time;
        SetActive(tr, true);
        if (fireAppear && tr.vfx && !string.IsNullOrEmpty(vfxEvent_OnAppear)) tr.vfx.SendEvent(vfxEvent_OnAppear);
    }

    void SetActive(Tracked tr, bool on)
    {
        tr.active = on;
        tr.tracker.gameObject.SetActive(on);
        tr.vfxTf.gameObject.SetActive(on);
    }
}
