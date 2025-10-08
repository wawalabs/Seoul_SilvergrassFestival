using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using OscSimpl;

public class PlayerPositionReceiver : MonoBehaviour
{
    

    [Header("OSC")]
    public OscIn oscIn;
    public int openPort = 12000;     // 0이면 자동 Open 안 함
    public string addrCount = "/Count";
    public string addrPosition = "/Position";
    [Tooltip("무신호 대기")]
    public float timeoutSec = 0.5f;

    [Header("좌표 매핑")]
    public float minX = -5f, maxX = 5f, minZ = -5f, maxZ = 5f;

    [Header("지터 저감")]
    public float smoothTime = 0.08f;

    [Header("최대 인식 인원")]
    public int maxPlayers = 15;

    [Header("프리팹")]
    [Tooltip("플레이어 위치 트래커(Visual 없음 권장, TransparentFX)")]
    public GameObject playerPrefab;
    [Tooltip("플레이어별 VFX 프리팹(VisualEffect 포함)")]
    public GameObject playerVfxPrefab;
    [Tooltip("군집 VFX 프리팹(동시에 여러 개)")]
    public GameObject clusterVfxPrefab;

    [Header("군집 조건")]
    public int clusterMinMembers = 3;
    public float clusterRadius = 1.5f;
    public int maxClusters = 8;

    [Header("VFX 키")]
    public string vfxParam_PlayerPos = "PlayerPos";
    public string vfxEvent_OnAppear = "OnAppear";
    public string vfxEvent_OnUpdate = "RipplePulse";
    public string vfxParam_ClusterCenter = "ClusterCenter";
    public string vfxEvent_ClusterBurst = "ClusterBurst";
    public string vfxEvent_ClusterSustain = "ClusterSustain";

    // ---------------- 내부 ----------------
    class TrackedPlayer
    {
        public Transform tracker;
        public VisualEffect vfx;
        public Transform vfxTf;
        public Vector3 vel;
        public float lastSeen;
        public bool active;
    }

    class ObjectPool
    {
        readonly GameObject prefab;
        readonly Transform parent;
        readonly Stack<GameObject> pool = new Stack<GameObject>();
        public ObjectPool(GameObject prefab, Transform parent = null, int preload = 0)
        {
            this.prefab = prefab; this.parent = parent;
            for (int i = 0; i < preload; i++) { var go = GameObject.Instantiate(prefab, parent); go.SetActive(false); pool.Push(go); }
        }
        public GameObject Get() { var go = pool.Count > 0 ? pool.Pop() : GameObject.Instantiate(prefab, parent); go.SetActive(true); return go; }
        public void Release(GameObject go) { go.SetActive(false); go.transform.SetParent(parent, false); pool.Push(go); }
    }

    readonly List<TrackedPlayer> players = new List<TrackedPlayer>();
    ObjectPool playerPool, playerVfxPool, clusterPool;

    readonly List<VisualEffect> activeClusterVfx = new List<VisualEffect>();
    readonly List<Transform> activeClusterTf = new List<Transform>();

    // ---------------- 라이프사이클 ----------------
    void Awake()
    {
        if (!playerPrefab) { Debug.LogError("playerPrefab 미지정"); enabled = false; return; }
        if (!playerVfxPrefab) { Debug.LogError("playerVfxPrefab 미지정"); enabled = false; return; }

        playerPool = new ObjectPool(playerPrefab, parent: transform, preload: Mathf.Max(0, maxPlayers));
        playerVfxPool = new ObjectPool(playerVfxPrefab, parent: transform, preload: Mathf.Max(0, maxPlayers));
        if (clusterVfxPrefab) clusterPool = new ObjectPool(clusterVfxPrefab, parent: transform, preload: Mathf.Max(1, maxClusters));
    }

    void Start()
    {
        if (!oscIn) { Debug.LogError("OscIn 미지정"); enabled = false; return; }
        oscIn.filterDuplicates = true;

        if (openPort > 0 && !oscIn.isOpen) oscIn.Open(openPort);  // 샘플과 동일 패턴 :contentReference[oaicite:2]{index=2}

        // 주소+메서드로 매핑 (API 시그니처 준수) :contentReference[oaicite:3]{index=3}
        oscIn.MapInt(addrCount, OnCount);
        oscIn.Map(addrPosition, OnPosition);
    }

    void OnDestroy()
    {
        if (oscIn == null) return;
        // 언맵은 메서드만 전달 (주소 미포함) :contentReference[oaicite:4]{index=4}
        oscIn.UnmapInt(OnCount);
        oscIn.Unmap(OnPosition);
    }

    void Update()
    {
        float now = Time.time;
        foreach (var p in players)
            if (p.active && now - p.lastSeen > timeoutSec)
                DeactivatePlayer(p);

        HandleClusters();
    }

    // ---------------- OSC ----------------
    void OnCount(int count)
    {
        count = Mathf.Clamp(count, 0, maxPlayers);
        EnsurePlayerCount(count);


        for (int i = 0; i < players.Count; i++)
        {
            bool on = i < count;
            if (on && !players[i].active) ActivatePlayer(players[i], fireAppearEvent: true);
            if (!on && players[i].active) DeactivatePlayer(players[i]);
        }
    }

    void OnPosition(OscMessage m)
    {
        int sensorCount = m.Count() / 2;
        int limit = Mathf.Min(sensorCount, players.Count);

        for (int i = 0; i < limit; i++)
        {
            if (!m.TryGet(i * 2, out float sx) || !m.TryGet(i * 2 + 1, out float sy)) continue;

            float px = Mathf.Lerp(minX, maxX, Mathf.Clamp01(sx));
            float pz = Mathf.Lerp(minZ, maxZ, Mathf.Clamp01(sy));

            var p = players[i];
            if (!p.active) ActivatePlayer(p, fireAppearEvent: true);

            Vector3 cur = p.tracker.position;
            Vector3 tgt = new Vector3(px, cur.y, pz);
            Vector3 next = Vector3.SmoothDamp(cur, tgt, ref p.vel, smoothTime);
            next.x = Mathf.Clamp(next.x, minX, maxX);
            next.z = Mathf.Clamp(next.z, minZ, maxZ);

            p.tracker.position = next;
            if (p.vfxTf) p.vfxTf.position = next;
            p.lastSeen = Time.time;

            if (p.vfx)
            {
                p.vfx.SetVector3(vfxParam_PlayerPos, next);
                if (!string.IsNullOrEmpty(vfxEvent_OnUpdate)) p.vfx.SendEvent(vfxEvent_OnUpdate);
            }
        }

        // OscSimpl은 Map(OscMessage) 사용 시 사용자 측에서 Recycle 권장 :contentReference[oaicite:5]{index=5}
        OscPool.Recycle(m);
    }

    // ---------------- 군집(다중) ----------------
    struct Cluster { public Vector3 center; public int size; }

    void HandleClusters()
    {
        var pts = new List<Vector3>();
        for (int i = 0; i < players.Count; i++)
            if (players[i].active) pts.Add(players[i].tracker.position);

        var clusters = FindClusters(pts, clusterRadius, clusterMinMembers, maxClusters);
        SyncClusterFxCount(clusters.Count);

        for (int i = 0; i < clusters.Count; i++)
        {
            var c = clusters[i];
            var tf = activeClusterTf[i];
            var fx = activeClusterVfx[i];

            tf.position = c.center;
            fx.SetVector3(vfxParam_ClusterCenter, c.center);
            if (!string.IsNullOrEmpty(vfxEvent_ClusterSustain)) fx.SendEvent(vfxEvent_ClusterSustain);
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

            var members = new List<int> { i };
            for (int j = 0; j < n; j++)
            {
                if (i == j || used[j]) continue;
                if ((pts[j] - pts[i]).sqrMagnitude <= r2) members.Add(j);
            }

            if (members.Count >= minMembers)
            {
                Vector3 sum = Vector3.zero;
                foreach (int idx in members) sum += pts[idx];
                Vector3 center = sum / members.Count;

                var members2 = new List<int>();
                for (int j = 0; j < n; j++)
                    if ((pts[j] - center).sqrMagnitude <= r2) members2.Add(j);

                if (members2.Count >= minMembers)
                {
                    sum = Vector3.zero;
                    foreach (int idx in members2) { sum += pts[idx]; used[idx] = true; }
                    center = sum / members2.Count;

                    clusters.Add(new Cluster { center = center, size = members2.Count });
                    if (clusters.Count >= maxClusters) break;
                }
            }
            else used[i] = true;
        }
        return clusters;
    }

    void SyncClusterFxCount(int needed)
    {
        while (activeClusterVfx.Count < needed)
        {
            GameObject go = clusterPool != null ? clusterPool.Get() : Instantiate(clusterVfxPrefab, transform);
            var fx = go.GetComponent<VisualEffect>();
            var tf = go.transform;

            if (!string.IsNullOrEmpty(vfxEvent_ClusterBurst)) fx.SendEvent(vfxEvent_ClusterBurst);

            activeClusterVfx.Add(fx);
            activeClusterTf.Add(tf);
        }

        for (int i = activeClusterVfx.Count - 1; i >= needed; i--)
        {
            var fx = activeClusterVfx[i];
            var go = fx.gameObject;
            activeClusterVfx.RemoveAt(i);
            activeClusterTf.RemoveAt(i);
            if (clusterPool != null) clusterPool.Release(go); else Destroy(go);
        }
    }

    // ---------------- 유틸 ----------------
    void EnsurePlayerCount(int target)
    {
        target = Mathf.Min(target, maxPlayers);

        while (players.Count < target)
        {
            var trackerGo = playerPool.Get();
            var vfxGo = playerVfxPool.Get();

            var tp = new TrackedPlayer
            {
                tracker = trackerGo.transform,
                vfx = vfxGo.GetComponent<VisualEffect>(),
                vfxTf = vfxGo.transform,
                vel = Vector3.zero,
                lastSeen = -999f,
                active = false
            };

            tp.tracker.position = Vector3.zero;
            tp.vfxTf.position = Vector3.zero;

            DeactivatePlayer(tp);
            players.Add(tp);
        }

        for (int i = players.Count - 1; i >= target; i--)
        {
            var p = players[i];
            if (p.active) DeactivatePlayer(p);
            playerPool.Release(p.tracker.gameObject);
            playerVfxPool.Release(p.vfxTf.gameObject);
            players.RemoveAt(i);
        }
    }

    void ActivatePlayer(TrackedPlayer p, bool fireAppearEvent)
    {
        p.active = true;
        p.lastSeen = Time.time;
        p.tracker.gameObject.SetActive(true);
        p.vfxTf.gameObject.SetActive(true);
        if (p.vfx && fireAppearEvent && !string.IsNullOrEmpty(vfxEvent_OnAppear))
            p.vfx.SendEvent(vfxEvent_OnAppear);
    }

    void DeactivatePlayer(TrackedPlayer p)
    {
        p.active = false;
        p.tracker.gameObject.SetActive(false);
        p.vfxTf.gameObject.SetActive(false);
    }
}
