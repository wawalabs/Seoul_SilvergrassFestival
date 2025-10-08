using UnityEngine;
using OscSimpl;
using UnityEngine.VFX;

public class PlayerPositionReceiverBackup : MonoBehaviour
{
    public OscIn oscIn;                  // OSC 컴포넌트
    public GameObject[] players;        // 레벨에 배치된 Player들
    public VisualEffect[] visualeffects;      // 각 Player에 대응하는 VFX

    [Header("플레이어 이동 범위")]
    public float minX = -5f;
    public float maxX = 5f;
    public float minZ = -5f;
    public float maxZ = 5f;

    void Start()
    {
        if (oscIn == null)
        {
            Debug.LogError("OscIn 컴포넌트가 지정되지 않았습니다.");
            enabled = false;
            return;
        }

        if (players.Length != visualeffects.Length)
        {
            Debug.LogError("players 배열과 visualeffects 배열의 길이가 일치하지 않습니다.");
            enabled = false;
            return;
        }

        // 시작 시 모든 플레이어와 리플을 비활성화
        for (int i = 0; i < players.Length; i++)
        {
            players[i].SetActive(false);
            if (visualeffects[i] != null) visualeffects[i].gameObject.SetActive(false);
        }

        // 센서 개수 수신
        oscIn.MapInt("/Count", OnCountReceived);
        // 위치 갱신을 하고 싶다면 여전히 남겨두기
        oscIn.Map("/Position", OnPositionReceived);
    }

    // 센서 개수 수신 시 실행
    void OnCountReceived(int count)
    {
        Debug.Log($"수신된 센서 개수: {count}");

        if (count > players.Length)
        {
            Debug.LogWarning($"센서 수({count})가 플레이어 수({players.Length})보다 많습니다. 초과분은 무시합니다.");
            count = players.Length;
        }

        for (int i = 0; i < players.Length; i++)
        {
            if (i < count)
            {
                players[i].SetActive(true);
                if (visualeffects[i] != null) visualeffects[i].gameObject.SetActive(true);
            }
            else
            {
                players[i].SetActive(false);
                if (visualeffects[i] != null) visualeffects[i].gameObject.SetActive(false);
            }
        }
    }

    // 위치 갱신을 하고 싶을 때만 사용
    void OnPositionReceived(OscMessage message)
    {
        int valueCount = message.Count();
        int sensorCount = valueCount / 2;

        for (int i = 0; i < players.Length && i < sensorCount; i++)
        {
            float sensorX, sensorY;
            if (!message.TryGet(i * 2, out sensorX) || !message.TryGet(i * 2 + 1, out sensorY))
            {
                Debug.LogWarning($"센서 {i} 값 읽기 실패");
                continue;
            }

            float playerX = Mathf.Lerp(minX, maxX, sensorX);
            float playerZ = Mathf.Lerp(minZ, maxZ, sensorY);

            players[i].transform.position = new Vector3(playerX, players[i].transform.position.y, playerZ);
        }
    }
}
